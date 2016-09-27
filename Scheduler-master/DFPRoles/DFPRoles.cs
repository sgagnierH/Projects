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

namespace DFPRoles
{
    public class DFPRoles: iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            process.log("Getting all Roles");
            // Get the users from DFP
            UserService userService = (UserService)interfaceUser.GetService(DfpService.v201508.UserService);
            userService.Timeout = 300000; // 5 min

            // Sets defaults for page and Statement.
            UserPage page = new UserPage();

            int success = 0;
            int retries = 0;
            Role[] roles = null;

            while (success == 0 && retries < 5)
            {
                try
                {
                    process.log("Loading...");
                    roles = userService.getAllRoles();
                    process.log("Loaded");
                    success = 1;
                }
                catch (Exception ex)
                {
                    retries++;
                    if (retries == 5)
                    {
                        process.log("Retrying: " + retries);
                        Db.sendError(process, ex.Message);
                        throw (new Exception("Unable to get roles", ex));
                    }
                }
            }

            int i = 0;
            if (roles != null && roles.Length > 0)
            {
                foreach (Role role in roles)
                {
                    process.log(++i + "/" + page.totalResultSetSize + " " + role.name);
                    DFPBase.checkDfpObject(process, "dfp_roles", "roleId", role);
                }
            }

            process.log("Number of items found: " + page.totalResultSetSize);
        }
    }
}
