using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201605;
using Google.Api.Ads.Dfp.v201605;
using com.hearst.utils;

namespace DealNews
{
    class DealNews
    {
        static void Main(string[] args)
        {
            DfpUser dfpUser = OAuth2.getDfpUser();

            string logfile = System.Configuration.ConfigurationManager.AppSettings["logfile"];
            logfile = logfile.Replace(".log", System.DateTime.Now.ToString("yyyyMMddHHmmss") + ".log");
            long customFieldId = Convert.ToInt64(System.Configuration.ConfigurationManager.AppSettings["customFieldId"]);
            string urlParam = System.Configuration.ConfigurationManager.AppSettings["urlParam"];

            log(logfile, "Getting customFieldOptions for Id " + customFieldId);
            Dictionary<long, string[]> customFieldOptions = getCustomFieldOptions(dfpUser, customFieldId, logfile, urlParam);
            if (customFieldOptions.Count == 0)
                throw new Exception("No customField defined with id " + customFieldId);
            log(logfile, "CustomFieldValues received");

            log(logfile, "Getting lineitems");
            Dictionary<long, long> lineitems = getLineItemsWithCustomField(dfpUser, customFieldId, logfile, customFieldOptions);
            if (lineitems.Count == 0)
                throw new Exception("No lineitems matching custom field " + customFieldId);
            log(logfile, "Lineitems received");

            log(logfile, "Getting creativeIds");
            Dictionary<long, long> creativeIds = getCreativeIds(dfpUser, logfile, lineitems);
            if (creativeIds.Count == 0)
                throw new Exception("No creatives matching custom field " + customFieldId);
            log(logfile, "creativeIds received");

            log(logfile, "Updating creatives");
            updateCreatives(dfpUser, logfile, customFieldOptions, lineitems, creativeIds);
            log(logfile, "Completed");
        }

        public static Dictionary<long, string[]> getCustomFieldOptions(DfpUser dfpUser, long customFieldId, string logfile, string urlParam)
        {
            // Get all CustomFieldOptions for the CustomField selected, and grab the piece of html from the web.
            // The drop down option values must be the url domain (eg: www.chron.com)
            // Returns dictionary of DropDownOptionIds and html snippet from webpage

            Dictionary<long, string[]> customFieldOptions = new Dictionary<long, string[]>();
            StatementBuilder statementBuilder;
            CustomFieldService service = (CustomFieldService)dfpUser.GetService(DfpService.v201605.CustomFieldService);
            CustomFieldPage page = null; ;
            bool success = false;
            int retries = 0;

            statementBuilder = new StatementBuilder()
                .OrderBy("id ASC")
                .Where("id = " + customFieldId);

            #region queryapi
            //This part will retry up to 5 times to query DFP API
            try
            {
                page = service.getCustomFieldsByStatement(statementBuilder.ToStatement());
                success = true;
            }
            catch (Exception ex)
            {
                log(logfile, "Exception : " + ex.Message);
                retries++;
                if (retries < 5)
                {
                    Console.WriteLine("Retry " + retries);
                }
                else
                {
                    log(logfile, ex.Message);
                }
            }
            #endregion
            if (success)
            {
                DropDownCustomField customField = (DropDownCustomField)page.results[0];
                foreach (CustomFieldOption customFieldOption in customField.options)
                {
                    // Making sure there is no trailing http(s):// to the url
                    Regex regex = new Regex("^(http(s)?)?://");
                    // Getting the html snippet form web site
                    string content = requestWebPage("http://" + regex.Replace(customFieldOption.displayName, "") + urlParam);
                    string[] levelDomains = customFieldOption.displayName.Split('.');
                    customFieldOptions.Add(customFieldOption.id, new string[] { levelDomains[1], content });
                }
            }
            return customFieldOptions;
        }
        public static Dictionary<long, long> getLineItemsWithCustomField(DfpUser dfpUser, long customFieldId, string logfile, Dictionary<long, string[]> customFieldOptions)
        {
            // Get all active lineitems with an endDateTime (not processing unlimitedEndDateTime lineitems)
            // Filtering out those who don't have the proper CustomFieldId set
            // Returns list of lineitemIds and their CustomFieldOptionId 

            Dictionary<long, long> lineItems = new Dictionary<long, long>();
            StatementBuilder statementBuilder;
            LineItemService service = (LineItemService)dfpUser.GetService(DfpService.v201605.LineItemService);
            LineItemPage page = null; ;
            bool success = false;
            int retries = 0;

            // There were no lineitemIds before 274657000 that had that condition, just lowering the number of results
            // Only get active lineitems with an endDateTime (no unlimitedEndDate)
            statementBuilder = new StatementBuilder()
                .OrderBy("id ASC")
                .Where("isArchived = false and id > 274657000 and startDateTime < '" + System.DateTime.Now.ToString("yyyy-MM-dd") + "' AND endDateTime > '" + System.DateTime.Now.ToString("yyyy-MM-dd") + "'")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            #region queryapi
            try
            {
                page = service.getLineItemsByStatement(statementBuilder.ToStatement());
                success = true;
            }
            catch (Exception ex)
            {
                log(logfile, "Exception : " + ex.Message);
                retries++;
                if (retries < 5)
                {
                    Console.WriteLine("Retry " + retries);
                }
                else
                {
                    log(logfile, ex.Message);
                }
            }
            #endregion
            if (success)
            {
                do
                {
                    foreach (LineItem lineitem in page.results)
                    {
                        if (lineitem.customFieldValues != null)
                        {
                            for (int i = 0; i < lineitem.customFieldValues.Length; i++)
                            {
                                if (lineitem.customFieldValues[i].customFieldId == customFieldId)
                                {
                                    if (!lineItems.ContainsKey(lineitem.id))
                                    {
                                        DropDownCustomFieldValue dropDownCustomFieldValue = (DropDownCustomFieldValue)lineitem.customFieldValues[i];
                                        lineItems.Add(lineitem.id, dropDownCustomFieldValue.customFieldOptionId);
                                    }
                                }
                            }
                        }
                    }
                    statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
                } while (statementBuilder.GetOffset() < page.totalResultSetSize);
            }
            return lineItems;
        }
        public static Dictionary<long, long> getCreativeIds(DfpUser dfpUser, string logfile, Dictionary<long, long> lineitems)
        {
            // Get the list of creativeIds matching the lineitems
            // Returns a list of creativeIds and associated lineitemId

            Dictionary<long, long> creativeIds = new Dictionary<long, long>();
            StatementBuilder statementBuilder;
            LineItemCreativeAssociationService service = (LineItemCreativeAssociationService)dfpUser.GetService(DfpService.v201605.LineItemCreativeAssociationService);
            LineItemCreativeAssociationPage page = null; ;
            bool success = false;
            int retries = 0;

            statementBuilder = new StatementBuilder()
                .OrderBy("id ASC")
                .Where("id in (" + String.Join(",", lineitems.Keys.ToArray()) + ")")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            #region queryapi
            try
            {
                page = service.getLineItemCreativeAssociationsByStatement(statementBuilder.ToStatement());
                success = true;
            }
            catch (Exception ex)
            {
                log(logfile, "Exception : " + ex.Message);
                retries++;
                if (retries < 5)
                {
                    Console.WriteLine("Retry " + retries);
                }
                else
                {
                    log(logfile, ex.Message);
                }
            }
            #endregion
            if (success)
            {
                do
                {
                    foreach (LineItemCreativeAssociation lica in page.results)
                    {
                        if (!creativeIds.ContainsKey(lica.creativeId))
                            creativeIds.Add(lica.creativeId, lica.lineItemId);
                    }
                    statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
                } while (statementBuilder.GetOffset() < page.totalResultSetSize);
            }
            return creativeIds;
        }
        public static void updateCreatives(DfpUser dfpUser, string logfile, Dictionary<long, string[]> customFieldOptions, Dictionary<long, long> lineitems, Dictionary<long, long> creativeIds)
        {
            // Does the actual update in DFP

            StatementBuilder statementBuilder;
            CreativeService service = (CreativeService)dfpUser.GetService(DfpService.v201605.CreativeService);
            CreativePage page = null; ;
            bool success = false;
            int retries = 0;

            statementBuilder = new StatementBuilder()
                .OrderBy("id ASC")
                .Where("id in (" + String.Join(",", creativeIds.Keys.ToArray()) + ")")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            #region queryapi
            try
            {
                page = service.getCreativesByStatement(statementBuilder.ToStatement());
                success = true;
            }
            catch (Exception ex)
            {
                log(logfile, "Exception : " + ex.Message);
                retries++;
                if (retries < 5)
                {
                    Console.WriteLine("Retry " + retries);
                }
                else
                {
                    log(logfile, ex.Message);
                }
            }
            #endregion
            if (success)
            {
                do
                {
                    foreach (CustomCreative creative in page.results)
                    {
                        // Update the creative to what should be in the final version
                        List<CustomCreative> toUpdate = new List<CustomCreative>();

                        // The snippet uses ul and li, we move this to divs
                        long lineitemId = creativeIds[creative.id];
                        long dropDownOptionId = lineitems[(int)lineitemId];
                        string domain = customFieldOptions[dropDownOptionId][0];
                        string dfpdomain = domain + System.Configuration.ConfigurationManager.AppSettings["apppenddomain"];
                        string content = customFieldOptions[dropDownOptionId][1].Replace("<li", "<div").Replace("</li>", "</div>");
                        int topPos = content.IndexOf("<ul");
                        topPos = content.IndexOf(">", topPos) + 1;
                        int botPos = content.IndexOf("</ul>");
                        string cleanedContent = content.Substring(topPos, botPos - topPos).Replace(domain, dfpdomain);
                        

                        // The part to update in the snippet is within feed comments
                        string snippet = creative.htmlSnippet;
                        int topSnip = snippet.IndexOf("<!-- feed -->") + 14;
                        int botSnip = snippet.IndexOf("<!-- /feed -->");

                        // The new snippet to publish
                        creative.htmlSnippet = snippet.Substring(0, topSnip) + "<!-- " + System.DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff") + " --><div id=\"cont\" class=\"cont\">" + cleanedContent + "</div>" + snippet.Substring(botSnip);

                        // Updating the creative
                        toUpdate.Add(creative);
                        int tryCount = 0;
                        bool done = false;
                        #region update DFP
                        // Will retry up to 5 times
                        while (!done && tryCount < 5)
                        {
                            try
                            {
                                service.updateCreatives(toUpdate.ToArray());
                                done = true;
                            }
                            catch (Exception ex)
                            {
                                tryCount++;
                                if (tryCount == 5)
                                {
                                    log(logfile, "Unable to update creative " + creative.id);
                                    throw new Exception("Unable to update creative " + creative.id, ex);
                                }
                            }
                        }
                        #endregion
                    }
                    statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
                } while (statementBuilder.GetOffset() < page.totalResultSetSize);
            }
        }

        // Utilities
        public static string requestWebPage(string url)
        {
            // Request a webpage via GET
            // Returns: requestWebPage content in string

            string webPageSource = null;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";

            int success = 0;
            int retries = 0;
            while (success == 0 && retries < 5)
            {
                try
                {
                    request.Timeout = 30000000;
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    if (response.StatusCode.ToString() == "OK")
                    {
                        Stream responseStream = response.GetResponseStream();
                        StreamReader myStreamReader = new StreamReader(responseStream, Encoding.Default);
                        webPageSource = myStreamReader.ReadToEnd();
                        myStreamReader.Close();
                        responseStream.Close();
                    }
                    success = 1;
                }
                catch (Exception ex)
                {
                    retries++;
                    if (retries == 5)
                        throw new Exception("Unable to get the page", ex);
                }
            }
            return webPageSource;
        }
        public static void log(string filename, string line)
        {
            // Display and log to file

            Console.WriteLine(System.DateTime.Now.ToString("HH:mm:ss.fff") + " " + line);
            if (filename == "")
                return;

            try
            {
                if (!File.Exists(filename))
                {
                    // Create a file to write to.
                    using (StreamWriter sw = File.CreateText(filename))
                    {
                        sw.WriteLine(line);
                    }
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(filename))
                    {
                        sw.WriteLine(line);
                    }
                }
            }
            catch(Exception)
            {
                Console.WriteLine("Unable to write to file " + filename);
            }
        }

    }
}
