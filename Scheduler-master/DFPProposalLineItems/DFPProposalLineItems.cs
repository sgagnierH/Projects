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

namespace DFPProposalLineItems
{
    public class DFPProposalLineItems : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            process.log("Getting all ProposalLineItems");

            // Get the Service.
            ProposalLineItemService service = (ProposalLineItemService)interfaceUser.GetService(DfpService.v201508.ProposalLineItemService);
            service.Timeout = 300000; // 5 min

            ProposalLineItemPage page = new ProposalLineItemPage();
            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id DESC")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            // Need to delete lines that are no more in DFP
            Db.execSqlCommand(process, "UPDATE dfp_proposallineitems SET working=1");

            do
            {
                // Get Proposals by Statement.
                process.log("Loading...");
                page = service.getProposalLineItemsByStatement(statementBuilder.ToStatement());
                process.log("Loaded ");

                if (page.results != null && page.results.Length > 0)
                {
                    int i = page.startIndex;
                    foreach (ProposalLineItem proposalLineItem in page.results)
                    {
                        process.log(++i + "/" + page.totalResultSetSize + " " + proposalLineItem.id + " : " + proposalLineItem.name);
                        DFPBase.checkDfpObject(process, "dfp_proposallineitems", "proposalLineItemId", proposalLineItem);

                        // We won't delete the lines we have gone through
                        Db.execSqlCommand(process, "UPDATE dfp_proposallineitems SET working=0 WHERE proposalLineItemId=" + proposalLineItem.id);
                    }
                }
                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            // Delete the lines that are in the database but not in DFP
            Db.execSqlCommand(process, "INSERT INTO dfp_proposallineitems_old SELECT * FROM dfp_proposallineitems WHERE working=1");
            Db.execSqlCommand(process, "DELETE FROM dfp_proposallineitems WHERE working=1");

            process.log("Number of items found: " + page.totalResultSetSize);
        }
    }
}
