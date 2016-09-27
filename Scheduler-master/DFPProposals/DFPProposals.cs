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

namespace DFPProposals
{
    public class DFPProposals : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            process.log("Getting all Proposals");

            // Get the Service.
            ProposalService service = (ProposalService)interfaceUser.GetService(DfpService.v201508.ProposalService);
            service.Timeout = 300000; // 5 min

            ProposalPage page = new ProposalPage();
            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id DESC")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            do
            {
                // Get Proposals by Statement.
                process.log("Loading...");
                page = service.getProposalsByStatement(statementBuilder.ToStatement());
                process.log("Loaded ");

                if (page.results != null && page.results.Length > 0)
                {
                    int i = page.startIndex;
                    foreach (Proposal proposal in page.results)
                    {
                        process.log(++i + "/" + page.totalResultSetSize + " " + proposal.id + " : " + proposal.name);
                        DFPBase.checkDfpObject(process, "dfp_proposals", "proposalId", proposal);
                    }
                }
                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            process.log("Number of items found: " + page.totalResultSetSize);
        }
    }
}
