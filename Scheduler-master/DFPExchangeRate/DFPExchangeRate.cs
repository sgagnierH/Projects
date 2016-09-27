using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;
using Google.Api.Ads.Common.Util;
using Tc.TcMedia.Scheduler;
using Tc.TcMedia.Dfp;
using MySql.Data.MySqlClient;

namespace DFPExchangeRate
{
    public class DFPExchangeRate : iScheduler
    {
        public void Run(Process process) 
        {
            // Tauc banque du Canada
            XmlDocument bdcx = new XmlDocument();
            bdcx.LoadXml(callUrl(process, "http://www.banqueducanada.ca/stats/assets/rates_rss/closing/fr_USD_CLOSE.xml"));
            string bdc = bdcx.SelectNodes("/").Item(0).ChildNodes[1].ChildNodes[1].ChildNodes[5].ChildNodes[1].ChildNodes[0].InnerText;

            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();
            ExchangeRateService exchangeRateService = (ExchangeRateService)interfaceUser.GetService(DfpService.v201508.ExchangeRateService);
            ExchangeRatePage page = new ExchangeRatePage();
            
            // Create a statement to select a single exchange rate by currency code.
            StatementBuilder statementBuilder = new StatementBuilder()
                .Where("currencyCode = 'USD'")
                .OrderBy("id ASC");

            try
            {
                // Get exchange rates by statement.
                page = exchangeRateService.getExchangeRatesByStatement(statementBuilder.ToStatement());

                if (page.results != null && page.results.Length > 0)
                {
                    int i = page.startIndex;
                    foreach (ExchangeRate exchangeRate in page.results)
                    {
                        Db.execSqlCommand(process, "INSERT INTO apn_exchangerate (cadToUsd, bdc, bdcCad2Usd) VALUES(" + exchangeRate.exchangeRate / 10000000000f + ", " + bdc + ", " + 1/Convert.ToDouble(bdc) + ")");
                        Console.WriteLine("{0}) currency code :'{1}', " +
                            " exchange rate '{2}' was found.", i++,
                             exchangeRate.currencyCode,
                            (exchangeRate.exchangeRate / 10000000000f));
                    }
                }
                Console.WriteLine("Number of results found: {0}", page.totalResultSetSize);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to get exchange rate by Statement. Exception says \"{0}\"",
                    e.Message);
            }
        }

        public string callUrl(Process process, string url)
        {
            string ret = null;
            HttpWebRequest request = null;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";

                int success = 0;
                int retries = 0;
                while (success == 0 && retries < 5)
                {
                    try
                    {
                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        if (response.StatusCode.ToString() == "OK")
                        {
                            Stream responseStream = response.GetResponseStream();

                            StreamReader myStreamReader = new StreamReader(responseStream, Encoding.Default);
                            ret = myStreamReader.ReadToEnd();

                            myStreamReader.Close();
                            responseStream.Close();
                        }
                        success = 1;
                    }
                    catch (Exception ex)
                    {
                        retries++;

                        process.log("Retrying " + retries + " - " + ex.Message);
                        if (retries == 5)
                            throw new Exception("Unable to reach url " + url, ex);
                    }
                }
            }
            catch(Exception ex)
            {
                throw new Exception("Unable to reach url " + url, ex);
            }
            return ret;
        }
    }
}
