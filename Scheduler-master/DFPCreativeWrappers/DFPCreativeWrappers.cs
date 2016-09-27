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

namespace DFPCreativeWrappers
{
    public class DFPCreativeWrappers : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            process.log("Getting all CreativesWrappers");

            // Get the CreativeService.
            CreativeWrapperService service = (CreativeWrapperService)interfaceUser.GetService(DfpService.v201508.CreativeWrapperService);

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id ASC")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            // Sets defaults for page and Statement.
            CreativeWrapperPage page = new CreativeWrapperPage();
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
                        page = service.getCreativeWrappersByStatement(statementBuilder.ToStatement());
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

                List<CreativeWrapper> creativeWrappers = new List<CreativeWrapper>();
                if (page.results != null && page.results.Length > 0)
                {
                    int i = page.startIndex;
                    foreach (CreativeWrapper creativeWrapper in page.results)
                    {
                        process.log(++i + "/" + page.totalResultSetSize + " : " + creativeWrapper.id);
                        DFPBase.checkDfpObject(process, "dfp_creativewrappers", "creativeWrapperId", creativeWrapper);

                        if (creativeWrapper.status == CreativeWrapperStatus.ACTIVE)
                        {
                            if (creativeWrapper.header.htmlSnippet.IndexOf("tcAdInfo") == -1)
                            {
                                if (creativeWrapper.header.htmlSnippet != "")
                                    creativeWrapper.header.htmlSnippet += "\n";
                                creativeWrapper.header.htmlSnippet += "<script>var tcAdInfo = {AdvertiserID : %eadv!,OrderID: %ebuy!,LineItemID: %eaid!,CreativeID: %ecid!,Geo: '%g',AdUnit1: '%s'};</script>";

                                creativeWrappers.Clear();
                                creativeWrappers.Add(creativeWrapper);
                                service.updateCreativeWrappers(creativeWrappers.ToArray());
                            }
                        }

                    }
                }
                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            process.log("Number of items found: " + page.totalResultSetSize);
        }
   }
}
