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

namespace DFPPlacements
{
    public class DFPPlacements : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            string lastModifiedDateTime = "";
            if (process.inDaySchedule) lastModifiedDateTime = Db.getLastModifiedDateTime(process, "dfp_placements");
            process.log("Getting all Placements" + ((lastModifiedDateTime == "") ? "" : " since " + lastModifiedDateTime));

            PlacementService service = (PlacementService)interfaceUser.GetService(DfpService.v201508.PlacementService);
            service.Timeout = Db.getTimeout();

            // Create a Statement to get all ad units.
            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id DESC")
                .Where((lastModifiedDateTime == "") ? "" : "lastModifiedDateTime > :lastModifiedDateTime")
                .AddValue("lastModifiedDateTime", lastModifiedDateTime)
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            // Set default for page.
            PlacementPage page = new PlacementPage();
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
                        page = service.getPlacementsByStatement(statementBuilder.ToStatement());
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
                    foreach (Placement placement in page.results)
                    {
                        process.log(++i + "/" + page.totalResultSetSize + " " + placement.id + " : " + placement.name);
                        DFPBase.checkDfpObject(process, "dfp_placements", "placementId", placement);

                        Db.execSqlCommand(process, "UPDATE dfp_placements_adunits SET working='" + process.guid + "' WHERE placementId=" + placement.id);
                        if(placement.targetedAdUnitIds != null)
                            foreach(string adunitId in placement.targetedAdUnitIds)
                                Db.execSqlCommand(process, "INSERT INTO dfp_placements_adunits (placementId, adunitId) values(" + placement.id + "," + adunitId + ") ON DUPLICATE KEY UPDATE working=null");

                        Db.execSqlCommand(process, "DELETE FROM dfp_placements_adunits WHERE working='" + process.guid + "' AND placementId=" + placement.id);
                    }
                }
                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            process.log("Number of items found: " + page.totalResultSetSize);
        }
    }
}
