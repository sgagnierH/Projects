using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tc.TcMedia.Scheduler;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System.Web.Services.Protocols;
using BIRPublish.com.birst.bws.app2103;

namespace BIRPublish
{
    public class BIRPublish : iScheduler
    {
        public StringBuilder sb = new StringBuilder();
        Process _thisprocess = null;
        Command cmd = null;

        public void Run(Process process)
        {
            cmd = JsonConvert.DeserializeObject<Command>(process.schedule.Config);
            CommandWebService cws = new CommandWebService();
            _thisprocess = process;
            string token = "";

            cws.Url = cmd.baseUrl + "/CommandWebService.asmx";
            cws.CookieContainer = new System.Net.CookieContainer(); // necessary to support cookie based load balancing 
            DateTime start = DateTime.Now;

            try
            {
                int retry = 0;
                bool success = false;
                //logit(0, process.schedule.Config);

                while (retry++ < 5 && !success)
                {
                    try
                    {
                        logit(retry, "Login");
                        token = cws.Login(cmd.username, cmd.password);
                        logit(retry, "Token : " + token);
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        System.Threading.Thread.Sleep(100);
                        logit(retry, "Error: " + ex.Message + " - Token : " + token);
                    }
                }
                if (!success)
                {
                    logit(retry, "Error after 5 retries - Token : " + token);
                    throw new Exception("Unable to log in");
                }

                foreach (string doit in cmd.groups.Split(','))
                {
                    publishData(cws, token, cmd.spaceId, new string[] { doit });
                }
            }
            catch (Exception ex)
            {
                logit(0, "Error : " + ex.Message);
            }
            finally
            {
                cws.Logout(token);
                logit(0, "Overall process took " + DateTime.Now.Subtract(start).TotalSeconds + " seconds.");

                string fileName = "C:\\temp\\birst_publish_" + System.DateTime.Now.ToString("yyyyMMdd_hhmmss") + ".log";
                File.WriteAllText(fileName, sb.ToString());
                Db.sendMail(process, cmd.toEmails, "Birst log for groups: " + cmd.groups, "Log attached", fileName);
                File.Delete(fileName);
            }
        }
        private void logit(int retry, string txt)
        {
            _thisprocess.log(retry + " - " + txt);
            sb.AppendLine(System.DateTime.Now.ToString("hh:mm:ss") + " " + retry + " - " + txt);
        }
        private void publishData(CommandWebService cws, string token, string spaceId, string[] subgroup)
        {
            string jobToken = "";
            int retry = 0;
            bool success = false;
            while (retry++ < 5 && !success)
            {
                try
                {
                    logit(retry, "Process " + subgroup[0]);
                    jobToken = cws.publishData(token, spaceId, subgroup, DateTime.Now);

                    bool complete = false;
                    DateTime now = DateTime.Now;
                    while (complete == false)
                    {
                        System.Threading.Thread.Sleep(60000);
                        complete = cws.isJobComplete(token, jobToken);
                    }
                    StatusResult status = cws.getJobStatus(token, jobToken);
                    logit(retry, "Process " + subgroup[0] + " took " + DateTime.Now.Subtract(now).TotalSeconds + " seconds to complete - Status message: " + ((status == null) ? "no status" : status.message));
                    success = true;
                }
                catch (Exception ex)
                {
                    logit(0, "Error: " + ex.Message + " Token : " + token + " - jobToken : " + jobToken);
                }                
            }
            if (!success)
            {
                logit(retry, "Error after 5 retries Token : " + token + " jobToken : " + jobToken);
                throw new Exception("Unable to process " + subgroup[0]);
            }
        }
    }
    #region helper classes
    public class Command
    {
        public string username { get; set; }
        public string password { get; set; }
        public string baseUrl { get; set; }
        public string spaceId { get; set; }
        public string groups { get; set; }
        public string toEmails { get; set; }

        public Command()
        {
            groups = ""; // Not null, will run with empty string if nothing is passed.
        }
    }
    #endregion
}
