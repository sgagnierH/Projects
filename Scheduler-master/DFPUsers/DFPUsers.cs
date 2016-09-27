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

namespace DFPUsers
{
    public class DFPUsers : iScheduler
    {
        private static Dictionary<long, string> roles = new Dictionary<long, string>();

        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            process.log("Getting all Users");
            // Get the users from DFP
            UserService userService = (UserService)interfaceUser.GetService(DfpService.v201508.UserService);
            userService.Timeout = 300000; // 5 min

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id DESC")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            bool hasErrors = false;
            // Set all users in dfp_users to "working"
            Db.execSqlCommand(process, "UPDATE dfp_users SET working='" + process.guid + "'");

            // Sets defaults for page and Statement.
            UserPage page = new UserPage();
            do
            {
                int success = 0;
                int retries = 0;
                while (success == 0 && retries < 5)
                {
                    try
                    {
                        process.log("Loading...");
                        page = userService.getUsersByStatement(statementBuilder.ToStatement());
                        process.log("Loaded");
                        success = 1;
                    }
                    catch (Exception ex)
                    {
                        hasErrors = true;
                        retries++;
                        if (retries == 5)
                        {
                            process.log("Retrying: " + retries);
                            Db.sendError(process, ex.Message);
                            throw (new Exception("Unable to get users", ex));
                        }
                    }
                }

                if (page.results != null && page.results.Length > 0)
                {
                    int i = page.startIndex;
                    foreach (User usr in page.results)
                    {
                        process.log(++i + "/" + page.totalResultSetSize + " " + usr.email);
                        DFPBase.checkDfpObject(process, "dfp_users", "userId", usr);
                        Db.execSqlCommand(process, "UPDATE dfp_users SET working=null WHERE userId=" + usr.id + " AND working='" + process.guid + "'");
                    }
                }
                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);
            process.log("Number of items found: " + page.totalResultSetSize);

            // Delete users that received no data, if no error occured.
            if (!hasErrors)
            {
                MySqlDataReader reader;
                StringBuilder sb = new StringBuilder();
                int nbDeleted = 0;
                reader = Db.getMySqlReader(process, "SELECT * FROM dfp_users WHERE working='" + process.guid + "'");
                while(reader.Read())
                {
                    nbDeleted++;
                    Db.execSqlCommand(process, "DELETE FROM dfp_users WHERE userId=" + reader.GetInt64("userId"));
                    sb.Append("User " + reader.GetString("name") + "(" + reader.GetString("email") + ") deleted");
                    process.log("User " + reader.GetString("name") + "(" + reader.GetString("email") + ") deleted");
                }
                reader.Close();
                if (sb.ToString() != "")
                {
                    Db.sendError(process, sb.ToString());
                    process.log("Number of users deleted: " + page.totalResultSetSize);
                }
            }
        }
    }
}
