using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;
using Google.Api.Ads.Common.Util;
using Tc.TcMedia.Scheduler;
using Tc.TcMedia.Dfp;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

namespace DFPNoTargetingMonitor2
{
    public class DFPNoTargetingMonitor2 : iScheduler
    {
        static Dictionary<string, long> Fields = new Dictionary<string, long>();

        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();
            long customTargetingKeyId = 266205; // header bidding

            //System.DateTime lastmodified = System.DateTime.UtcNow.AddDays(-1);
            //string lastModifiedDateTime = lastmodified.ToString("yyyy-MM-ddThh:mm:ss");

            LineItemService lineItemService = (LineItemService)interfaceUser.GetService(DfpService.v201508.LineItemService);
            process.log("Getting all lineitems"); // modified since " + lastModifiedDateTime);

            // Create a Statement to get all line items.
            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id DESC")
                .Where("status = :status") // AND lastModifiedDateTime > :lastModifiedDateTime")
                .AddValue("status", "DELIVERING")
                //.AddValue("lastModifiedDateTime", lastModifiedDateTime)
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            // Sets default for page.
            LineItemPage page = new LineItemPage();
            int totalnum = 1;
            StringBuilder sb = new StringBuilder();

            do
            {
                // Get line items by Statement.
                process.log("Loading... ");
                int retries = 0;
                int success = 0;
                while (success == 0 && retries < 5)
                {
                    try
                    {
                        page = lineItemService.getLineItemsByStatement(statementBuilder.ToStatement());
                        success = 1;
                    }
                    catch (Exception ex)
                    {
                        retries++;
                        if (retries == 5)
                        {
                            process.log("Retry " + retries);
                            Db.sendError(process, ex.Message);
                        }
                    }
                }
                totalnum = page.totalResultSetSize;

                if (page.results != null && page.results.Length > 0)
                {
                    int i = page.startIndex;
                    foreach (LineItem lineItem in page.results)
                    {
                        process.log(++i + "/" + page.totalResultSetSize + " " + lineItem.name);
                        bool inventoryTargeted = false;
                        
                        if (lineItem.targeting.inventoryTargeting.targetedAdUnits != null)
                            foreach (AdUnitTargeting aut in lineItem.targeting.inventoryTargeting.targetedAdUnits)
                            {
                                if (aut.adUnitId.ToString() != "20599605")
                                    inventoryTargeted = true;
                            }

                        bool headerBidding = false;
                        // lineItem Type
                        if (!inventoryTargeted && lineItem.customFieldValues != null)
                        {
                            foreach (var custom in lineItem.customFieldValues)
                            {
                                if (custom.customFieldId == 9525) // 9525 : lineItem Type
                                {
                                    DropDownCustomFieldValue cf = (DropDownCustomFieldValue)custom;
                                    if (cf.customFieldOptionId == 29925) // Header Bidding
                                    {
                                        headerBidding = true;
                                    }
                                }
                            }
                        }
                        //if (!inventoryTargeted && lineItem.targeting.customTargeting != null)
                        //    if (lineItem.targeting.customTargeting.GetType().FullName == "Google.Api.Ads.Dfp.v201508.CustomCriteriaSet")
                        //        try
                        //        {
                        //            foreach (CustomCriteria customCriteria in ((Google.Api.Ads.Dfp.v201508.CustomCriteriaSet)(lineItem.targeting.customTargeting.children[0])).children)
                        //                if (customCriteria.keyId == customTargetingKeyId)
                        //                    headerBidding = true;
                        //        }
                        //        catch(Exception)
                        //        {
                        //            //process.log(ex.Message);
                        //        }

                        if (!inventoryTargeted && !headerBidding && lineItem.targeting.inventoryTargeting.targetedPlacementIds == null)
                        {
                            string Output = lineItem.id + ",\"" + lineItem.name + "\"," + lineItem.status.ToString() + ",https://www.google.com/dfp/4916#delivery/LineItemDetail/lineItemId=" + lineItem.id + "\n";
                            sb.AppendLine(Output);
                        }
                    }
                }

                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            if(sb.Length > 0)
            {
                sendNotificationMail(process, sb.ToString());
                process.log("LineItems not targeting: \n" + sb.ToString());

            }
        }

        static void sendNotificationMail(Process process, string content)
        {
            Command cmd = JsonConvert.DeserializeObject<Command>(process.schedule.Config);
            Db.sendMail(process, cmd.toEmail, cmd.title, cmd.body + "\n\n" + content);
        }
    }
    class Command
    {
        public string title { get; set; }
        public string body { get; set; }
        public string toEmail { get; set; }
    }
}