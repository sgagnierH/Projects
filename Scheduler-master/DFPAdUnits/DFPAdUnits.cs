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

namespace DFPAdUnits
{
    public class DFPAdUnits : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            string lastModifiedDateTime = "";
            if (process.inDaySchedule) lastModifiedDateTime = Db.getLastModifiedDateTime(process, "dfp_adunits");
            process.log("Getting all AdUnits" + ((lastModifiedDateTime == "") ? "" : " since " + lastModifiedDateTime));

            InventoryService inventoryService = (InventoryService)interfaceUser.GetService(DfpService.v201508.InventoryService);
            inventoryService.Timeout = Db.getTimeout();

            // Create a Statement to get all ad units.
            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id DESC")
                .Where((lastModifiedDateTime == "") ? "" : "lastModifiedDateTime > :lastModifiedDateTime")
                .AddValue("lastModifiedDateTime", lastModifiedDateTime)
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            // Set default for page.
            AdUnitPage page = new AdUnitPage();
            int i = 0;
            do
            {
                int success = 0;
                int retries = 0;
                while (success == 0 && retries < 5)
                {
                    try
                    {
                        process.log("Loading...");
                        page = inventoryService.getAdUnitsByStatement(statementBuilder.ToStatement());
                        process.log(" .. Loaded ");
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

                if (page.results != null && page.results.Length > 0)
                {
                    foreach (AdUnit adUnit in page.results) 
                    {
                        process.log(++i + "/" + page.totalResultSetSize + " " + adUnit.id + " : " + adUnit.name);
                        DFPBase.checkDfpObject(process, "dfp_adunits", "adunitId", adUnit);
                    }
                }
                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            process.log("Number of items found: " + page.totalResultSetSize);
        }
    }
}
