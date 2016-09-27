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

namespace DFPTeams
{
    public class DFPTeams : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            Console.WriteLine("Getting all Teams");
            // Get the TeamService.
            TeamService teamService = (TeamService)interfaceUser.GetService(DfpService.v201508.TeamService);
            teamService.Timeout = 300000; // 5 min

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id DESC")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            // Set defaults for page and filterStatement.
            TeamPage page = new TeamPage();

            do
            {
                // Get teams by statement.
                process.log("Loading..."); 
                page = teamService.getTeamsByStatement(statementBuilder.ToStatement());
                process.log("Loaded ");

                if (page.results != null)
                {
                    int i = page.startIndex;
                    foreach (Team team in page.results)
                    {
                        process.log(++i + "/" + page.totalResultSetSize + " " + team.name);
                        DFPBase.checkDfpObject(process, "dfp_teams", "teamId", team);
                    }
                }
                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            process.log("Number of items found: " + page.totalResultSetSize);
        }
    }
}
