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

namespace DFPUsersTeams
{
    public class DFPUsersTeams : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            process.log("Assigning teams to users");

            // Get the UserTeamAssociationService.
            UserTeamAssociationService userTeamAssociationService = (UserTeamAssociationService)interfaceUser.GetService(DfpService.v201508.UserTeamAssociationService);
            userTeamAssociationService.Timeout = 300000; // 5 min

            // Set defaults for page and filterStatement.
            UserTeamAssociationPage page = new UserTeamAssociationPage();
            Statement filterStatement = new Statement();
            int offset = 0;

            do
            {
                // Create a statement to get all user team associations.
                filterStatement.query = "LIMIT 500 OFFSET " + offset;

                // Get user team associations by statement.
                process.log("Loading");
                page = userTeamAssociationService.getUserTeamAssociationsByStatement(filterStatement);
                process.log("Loaded");

                if (page.results != null)
                {
                    int i = page.startIndex;
                    foreach (UserTeamAssociation userTeamAssociation in page.results)
                    {
                        checkUserTeam(process, userTeamAssociation.userId, userTeamAssociation.teamId);
                        i++;
                    }
                }

                offset += 500;
            } while (offset < page.totalResultSetSize);

            process.log("Number of items found: " + page.totalResultSetSize);
        }

        public void checkUserTeam(Process process, long userId, long teamId)
        {
            MySqlDataReader dataReader = Db.getMySqlReader(process, "Select * from dfp_teams_links where userId = " + userId);
            if (dataReader.HasRows)
            {
                bool found = false;
                while (dataReader.Read())
                {
                    if (dataReader.GetInt64("teamId") == teamId)
                    {
                        found = true;
                        if (dataReader.GetInt16("deleted") == 1)
                            updateTeamLinksId(process, dataReader.GetInt64("team_linksId"), 0);
                    }
                }
                if (!found)
                    DFPBase.applyTeam(process, Db.NULL, teamId, Db.NULLLONG, Db.NULLLONG, Db.NULLLONG, userId);
            }
            else
                DFPBase.applyTeam(process, Db.NULL, teamId, Db.NULLLONG, Db.NULLLONG, Db.NULLLONG, userId);

            dataReader.Close();
        }
        private void updateTeamLinksId(Process process, long teamLinksId, int value)
        {
            Db.execSqlCommand(process, "UPDATE dfp_teams_links SET deleted=" + value + " WHERE team_linksId = " + teamLinksId);
            Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValue,Info,Data) VALUES('teamLinksId'," + teamLinksId + ",'Update','deleted=" + value + "')");
        }

    }
}
