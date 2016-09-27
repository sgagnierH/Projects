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

namespace DFPCreativeTemplates
{
    public class DFPCreativeTemplates : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            string lastModifiedDateTime = "";
            //if (process.inDaySchedule) lastModifiedDateTime = Db.getLastModifiedDateTime(process, "dfp_creatives", 1);
            process.log("Getting all Creatives" + ((lastModifiedDateTime == "") ? "" : " since " + lastModifiedDateTime));

            // Get the CreativeService.
            CreativeTemplateService service = (CreativeTemplateService)interfaceUser.GetService(DfpService.v201508.CreativeTemplateService);
            service.Timeout = Db.getTimeout();

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id DESC")
                .Where((lastModifiedDateTime == "") ? "" : "lastModifiedDateTime > :lastModifiedDateTime ")
                .AddValue("lastModifiedDateTime", lastModifiedDateTime)
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            // Sets defaults for page and Statement.
            CreativeTemplatePage page = new CreativeTemplatePage();
            do
            {
                // Get Creatives by Statement.
                int success = 0;
                int retries = 0;
                while(success == 0 && retries < 5)
                {
                    try
                    {
                        process.log(System.DateTime.Now.ToString(Db.TIME_FORMAT) + " Loading...");
                        page = service.getCreativeTemplatesByStatement(statementBuilder.ToStatement());
                        process.log(" .. Loaded " + System.DateTime.Now.ToString(Db.TIME_FORMAT));
                        success = 1;
                    }
                    catch(Exception ex)
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
                    int i = page.startIndex;
                    foreach (CreativeTemplate creativeTemplate in page.results)
                    {
                        process.log(++i + "/" + page.totalResultSetSize + " : " + creativeTemplate.id + " - " + creativeTemplate.name);
                        //DFPBase.checkDfpObject(process, "dfp_creativeTemplates", "creativeTemplateId", creativeTemplate);
                    }
                }
                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            process.log("Number of items found: " + page.totalResultSetSize);
        }
   }
}
