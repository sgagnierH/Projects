using System;
using System.Reflection;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;
using Google.Api.Ads.Common.Util;
using MySql.Data.MySqlClient;
using System.Net;
using System.Net.Mail;
using Newtonsoft.Json;

namespace Tc.TcMedia.Scheduler
{
    public interface iScheduler
    {
        void Run(Process process);
    }
    public interface iDFPReportDownloader
    {
        void define(ReportConfig config, string timespan);
        void postProcess(ReportConfig config, string timespan);
    }
    public class Db
    {
        public static string connectionString;
        public const long NULLLONG = -9999999;
        public const string NULL = null;
        public const string TIME_FORMAT = "hh:mm:ss";
        public const string DATETIME_FORMAT = "yyyyMMdd";
        public const string DATETIME_FORMAT_FULL = "yyyyMMddhhmmss";
        private static Dictionary<string, Config> config = new Dictionary<string, Config>();

        public Db(Process process)
        {
            connectionString = "SERVER=tcadops.cfcijtwmbc7u.us-east-1.rds.amazonaws.com;PORT=8080;DATABASE=tcadops;UID=tcadopsDb;PASSWORD=M4mm0uth$;"; // System.Configuration.ConfigurationManager.ConnectionStrings["dfp"].ConnectionString;

            if (config.Count == 0)
            {
                MySqlConnection conn = getConnection();

                MySqlDataReader dataReader = null;
                MySqlCommand command = new MySqlCommand();
                command.Connection = conn;
                command.CommandText = "Select * from 0_config";

                try
                {
                    if (command.Connection.State != System.Data.ConnectionState.Open)
                        command.Connection.Open();

                    dataReader = command.ExecuteReader();
                }
                catch (Exception ex)
                {
                    process.log(ex.Message);
                }

                while (dataReader.Read())
                {
                    Config aconfig = new Config();
                    aconfig.ConfigId = dataReader.GetInt64("configId");
                    aconfig.Name = dataReader.GetString("name");
                    aconfig.Type = dataReader.GetString("type");
                    aconfig.Value = dataReader.GetString("value");
                    config.Add(aconfig.Name, aconfig);
                }
                dataReader.Close();
                conn.Close();
            }
        }

        public static int execCmd(string path, string args, out string stdout, out string stderr)
        {
            var proc = new System.Diagnostics.Process();

            proc.StartInfo.Arguments = args;
            proc.StartInfo.FileName = path;
            proc.Start();
            proc.WaitForExit();
            stdout = proc.StandardOutput.ReadToEnd();
            stderr = proc.StandardError.ReadToEnd();
            return proc.ExitCode;
        }
        #region reports
        public static void setReportSuccess(Process process)
        {
            execSqlCommand(process, "UPDATE 0_report_queue SET running=null, status=1, errorCount=0, errorMessage='' WHERE reportQueueId=" + process.report.reportQueueId);
        }
        public static void setReportFailure(Process process, string Message)
        {
            execSqlCommand(process, "UPDATE 0_report_queue SET running=null, status=-1, errorCount=errorCount+1, errorMessage='" + Message.Replace("'", "''") + "' WHERE reportQueueId=" + process.report.reportQueueId);
        }
        public static bool alreadyInReportQueue(Process process, long orderId, System.DateTime date, string source)
        {
            bool ret = false;
            MySqlConnection conn = getConnection();
            MySqlDataReader dr = null;

            try
            {
                dr = getMySqlReader(process, "SELECT count(*) as nb FROM 0_report_queue WHERE orderId=" + orderId + " AND date='" + toMySqlDate(date) + "' AND source='" + source + "'", conn);
                if (dr.Read())
                    if (dr.GetInt64("nb") == 1) ret = true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                dr.Close();
                conn.Close();
            }

            return ret;
        }
        public static Report getReport(Process process)
        {
            Report ret = null;
            process.report = ret;

            MySqlDataReader dataReader = getMySqlReader(process, "Select reportQueueId from 0_report_queue WHERE status < 1 AND running is NULL ORDER BY errorCount asc, reportQueueId ASC LIMIT 1");
            if (dataReader.Read())
            {
                long reportQueueId = dataReader.GetInt64("reportQueueId");
                execSqlCommand(process, "UPDATE 0_report_queue SET running='" + process.guid.ToString() + "' WHERE reportQueueId=" + reportQueueId + " AND running IS NULL");
                dataReader.Close();

                dataReader = getMySqlReader(process, "Select * from 0_report_queue_v WHERE running='" + process.guid.ToString() + "' AND reportQueueId=" + reportQueueId);
                if (dataReader.Read())
                {
                    try
                    {
                        ret = new Report();
                        ret.reportQueueId = reportQueueId;
                        ret.lang = dataReader.GetString("lang");
                        ret.orderId = dataReader.GetInt64("orderId");
                        if (dataReader.GetString("sfo_orderId") != "")
                            ret.sfo_orderId = dataReader.GetString("sfo_orderId");
                        if (dataReader.GetString("orderNo") != "")
                            ret.orderNo = dataReader.GetString("orderNo");
                        ret.source = dataReader.GetString("source");
                        ret.options = JsonConvert.DeserializeObject<ReportOptions>(dataReader.GetString("options"));
                        ret.emails = dataReader.GetString("emails");
                        ret.reportTemplateName = dataReader.GetString("reportTemplateName");
                        ret.title = dataReader.GetString("title");
                        ret.body = dataReader.GetString("body");
                        ret.name = dataReader.GetString("name");
                        ret.startDateTime = dataReader.GetDateTime("startDateTime");
                        ret.unlimitedEndDateTime = (dataReader.GetString("unlimitedEndDateTime") == "1");
                        if (!ret.unlimitedEndDateTime)
                            ret.endDateTime = dataReader.GetDateTime("endDateTime");
                        ret.poNumber = dataReader.GetString("poNumber");
                        ret.salespersonId = dataReader.GetInt64("salespersonId");
                        ret.status = dataReader.GetString("status");
                        ret.totalBudget = dataReader.GetInt64("totalBudget");
                        ret.salespersonName = dataReader.GetString("salespersonName");
                        ret.salespersonEmail = dataReader.GetString("salespersonEmail");
                        ret.advertiser = dataReader.GetString("advertiser");
                    }
                    catch(Exception ex)
                    {
                        throw ex;
                    }
                    process.report = ret;
                }
            }

            dataReader.Close();
            if(ret != null)
                if(ret.source == "APN")
                {
                    dataReader = getMySqlReader(process, "SELECT MIN(start_date) as startDate, MAX(end_date) as endDate FROM apn_lineitems_v WHERE orderId=" + ret.orderId);
                    if(dataReader.Read())
                    {
                        ret.startDateTime = dataReader.GetDateTime("startDate");
                        ret.endDateTime = dataReader.GetDateTime("endDate");
                    }
                    dataReader.Close();
                }
            return ret;
        }
        #endregion

        #region schedule
        public static void setSuccess(Process process)
        {
            execSqlCommand(process, "UPDATE 0_scheduler SET lastRun='" + toMySqlDate(System.DateTime.Now) + "', running=null, errorCount=0, errorMessage='', nextRun='" + toMySqlDate(process.schedule.CalculatedNextRun) + "', lastSuccessfulRun='" + toMySqlDate(process.schedule.NextRun) + "' WHERE scheduleId=" + process.schedule.ScheduleId);
            if(process.schedule.RepeatInterval == "NONE")
            {
                execSqlCommand(process, "INSERT INTO 0_scheduler_completed SELECT * FROM 0_scheduler WHERE scheduleId=" + process.schedule.ScheduleId);
                execSqlCommand(process, "DELETE FROM 0_scheduler  WHERE scheduleId=" + process.schedule.ScheduleId);
            }
        }
        public static void setFailure(Process process, string Message)
        {
            execSqlCommand(process, "UPDATE 0_scheduler SET lastRun='" + toMySqlDate(System.DateTime.Now) + "', running=null, errorCount=" + (++process.schedule.ErrorCount) + ", errorMessage='" + Message.Replace("'", "''") + "' WHERE scheduleId=" + process.schedule.ScheduleId);
        }
        public static Schedule fillSchedule(MySqlDataReader dataReader)
        {
            Schedule retSchedule = new Schedule();
            retSchedule.ScheduleId = dataReader.GetInt64("scheduleId");
            retSchedule.Config = dataReader.GetString("config");
            retSchedule.Name = dataReader.GetString("schedule");
            retSchedule.Enabled = dataReader.GetBoolean("enabled");
            retSchedule.NextRun = dataReader.GetDateTime("nextRun");
            retSchedule.RepeatEach = dataReader.GetInt32("repeatEach");
            retSchedule.RepeatInterval = dataReader.GetString("repeatInterval");
            retSchedule.LastRun = dataReader.GetDateTime("lastRun");
            retSchedule.ErrorCount = dataReader.GetInt32("errorCount");
            retSchedule.ErrorMessage = dataReader.GetString("errorMessage");
            retSchedule.RunAllSteps = dataReader.GetBoolean("runAllSteps");
            return retSchedule;
        }
        public static Schedule getSchedule(Process process, string schedule)
        {
            Schedule retSchedule = null;
            string running = (System.Diagnostics.Debugger.IsAttached) ? "" : " AND running IS NULL";

            MySqlDataReader dataReader = getMySqlReader(process, "Select scheduleId from 0_scheduler WHERE schedule='" + schedule + "'");
            if (dataReader.Read())
            {
                long scheduleId = dataReader.GetInt64("scheduleId");
                execSqlCommand(process, "UPDATE 0_scheduler SET running='" + process.guid.ToString() + "' WHERE scheduleId=" + scheduleId + running);
                dataReader.Close();

                dataReader = getMySqlReader(process, "Select * from 0_scheduler WHERE running='" + process.guid.ToString() + "' AND scheduleId=" + scheduleId);
                if (dataReader.Read())
                    retSchedule = fillSchedule(dataReader);
            }

            dataReader.Close();
            process.schedule = retSchedule;
            return retSchedule;
        }
        public static Schedule getNextSchedule(Process process)
        {
            Schedule retSchedule = null;
            string group = " WHERE `group`=" + process.group;
            string tableExt = "";
            if (System.Diagnostics.Debugger.IsAttached)
            {
                tableExt = "_debug";
                group = "";
            }

            MySqlDataReader dataReader = getMySqlReader(process, "Select * from 0_scheduler_v" + tableExt + group + " LIMIT 1");
            if (dataReader.Read())
            {
                long scheduleId = dataReader.GetInt64("scheduleId");
                execSqlCommand(process, "UPDATE 0_scheduler SET running='" + process.guid.ToString() + "' WHERE scheduleId=" + scheduleId + " AND running IS NULL");
                dataReader.Close();

                dataReader =  getMySqlReader(process, "Select * from 0_scheduler WHERE running='" + process.guid.ToString() + "' AND scheduleId=" + scheduleId);
                if (dataReader.Read())
                    retSchedule = fillSchedule(dataReader);
            }
                
            dataReader.Close();
            process.schedule = retSchedule;
            return retSchedule;
        }
        public static int getTimeout()
        {
            int timeout = 300000;
            if(config.ContainsKey("timeout"))
                timeout = (int)config["Timeout"].getValue;

            return timeout;
        }
        public static bool inDaySchedule()
        {
            int NightStartHour = (config.ContainsKey("NightStartHour")) ? Convert.ToInt16(config["NightStartHour"].getValue) : 0;
            int NightEndHour = (config.ContainsKey("NightEndHour")) ? Convert.ToInt16(config["NightEndHour"].getValue) : 6;
            int currentHour = System.DateTime.Now.Hour;
            return !(NightStartHour < currentHour && currentHour < NightEndHour);   
        }
        public static string getLastModifiedDateTime(Process process, string table, int days = 0)
        {
            string ret = toMySqlDate(System.DateTime.MinValue);
            MySqlConnection conn = getConnection();
            MySqlDataReader dataReader = null;
            try
            {
                dataReader = getMySqlReader(process, "SELECT MAX(lastModifiedDateTime) AS lastModifiedDateTime FROM " + table);
                if (dataReader.Read())
                    ret = toMySqlDate(((System.DateTime)(dataReader["lastModifiedDateTime"])).AddDays(-days)).Replace(" ", "T");
            }
            catch (Exception) 
            {
                ret = "2000-01-01T00:00:00";
            }
            finally
            {
                dataReader.Close();
                conn.Close();
            }
            return ret;
        }
        #endregion

        #region dateconversion
        public static bool DifferentDate(System.DateTime mySqlDateTime, Google.Api.Ads.Dfp.v201508.DateTime value)
        {
            bool ret = false;
            if (mySqlDateTime.Year != value.date.year) ret = true;
            if (mySqlDateTime.Month != value.date.month) ret = true;
            if (mySqlDateTime.Day != value.date.day) ret = true;
            if (mySqlDateTime.Hour != value.hour) ret = true;
            if (mySqlDateTime.Minute != value.minute) ret = true;
            if (mySqlDateTime.Second != value.second) ret = true;
            return ret;
        }
        public static string SystemDateTimeToMySqlDate(System.DateTime dateTime)
        {
            string ret = "";
            if (dateTime != null)
            {
                ret += dateTime.Year.ToString("0000");
                ret += "-";
                ret += dateTime.Month.ToString("00");
                ret += "-";
                ret += dateTime.Day.ToString("00");
                ret += " ";
                ret += dateTime.Hour.ToString("00");
                ret += ":";
                ret += dateTime.Minute.ToString("00");
                ret += ":";
                ret += dateTime.Second.ToString("00");
            }
            return ret;
        }
        public static string toMySqlDate(System.DateTime dateTime)
        {
            string ret = "";
            if (dateTime != null)
            {
                ret += dateTime.Year.ToString("0000");
                ret += "-";
                ret += dateTime.Month.ToString("00");
                ret += "-";
                ret += dateTime.Day.ToString("00");
                ret += " ";
                ret += dateTime.Hour.ToString("00");
                ret += ":";
                ret += dateTime.Minute.ToString("00");
                ret += ":";
                ret += dateTime.Second.ToString("00");
            }
            return ret;
        }
        #endregion

        #region helper
        public static bool isNumeric(string inputString)
        {
            Encoding from = Encoding.Default;
            {
                foreach (char c in inputString)
                {
                    if (c < '0' || c > '9')
                        return false;
                }

                return true;
            }
        }
        public static string getConfig(string name, dynamic defaultValue)
        {
            if (config.ContainsKey(name))
                return config[name].Value;
            else
                return defaultValue;
        }
        public static string MakeValidFileName(string name)
        {
            /*string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars())) + "'";
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            byte[] tempBytes = System.Text.Encoding.GetEncoding("ISO-8859-8").GetBytes(System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, ""));
            return System.Text.Encoding.UTF8.GetString(tempBytes).Replace(" ", "_");
             */
            string res = Regex.Replace(name, @"[^\p{L}\p{N}\s\-]+", "");
            return res.Replace(" ", "_");
        }
        #endregion

        #region mysql
        public static void execSqlCommand(Process process, string strSql)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = getConnection();
            cmd.CommandTimeout = 3000;
            cmd.CommandText = strSql;

            int success = 0;
            int retries = 0;
            while (success == 0 && retries < 5)
            {
                try
                {
                    if (cmd.Connection.State != System.Data.ConnectionState.Open) cmd.Connection.Open();
                    cmd.ExecuteNonQuery();
                    cmd.Connection.Close();
                    success = 1;
                }
                catch (Exception ex)
                {
                    retries++;
                    System.Threading.Thread.Sleep(100);
                    if (retries == 5)
                    {
                        process.log(ex.Message);
                        process.log("---> " + strSql);
                        throw new Exception("Error executing MySql statement", ex);
                    }
                }
            }
        }
        public static void execSqlCommand(Process process, MySqlConnection conn, string strSql)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;
            cmd.CommandTimeout = 300;
            cmd.CommandText = strSql;

            int success = 0;
            int retries = 0;
            while (success == 0 && retries < 5)
            {
                try
                {
                    if (conn.State != System.Data.ConnectionState.Open) cmd.Connection.Open();
                    cmd.ExecuteNonQuery();
                    success = 1;
                }
                catch (Exception ex)
                {
                    retries++;
                    System.Threading.Thread.Sleep(100);
                    if (retries == 5)
                    {
                        process.log(ex.Message);
                        process.log("---> " + strSql);
                        throw new Exception("Error executing MySql statement", ex);
                    }
                }
            }
        }
        public static MySqlDataReader getMySqlReader(Process process, string strSql, MySqlConnection altConn = null)
        {
            MySqlDataReader dataReader = null;
            MySqlCommand command = new MySqlCommand();
            command.Connection = process.conn;
            command.CommandText = strSql;
            command.CommandTimeout = 5000;

            if(altConn != null)
                command.Connection = altConn;

            int success = 0;
            int retries = 0; 
            while (success == 0 && retries < 5)
            {
                try
                {
                    if (command.Connection.State != System.Data.ConnectionState.Open) command.Connection.Open();
                    dataReader = command.ExecuteReader();
                    success = 1;
                }
                catch (Exception ex)
                {
                    retries++;
                    if (dataReader != null && !dataReader.IsClosed)
                        dataReader.Close();
                    System.Threading.Thread.Sleep(100);
                    if (retries == 5)
                    {
                        process.log(ex.Message);
                        throw new Exception("Error getting Reader", ex);
                    }
                }
            }
            return dataReader;
        }
        public static MySqlConnection getConnection()
        {
            return new MySqlConnection(connectionString);
        }
        public static string getFieldName(string dfpName)
        {
            string ret = "";
            bool toUppercase = false;
            for (int i = 0; i < dfpName.Length; i++)
            {
                string character = dfpName.Substring(i, 1).Replace("%","Pct");
                if (i == 0)
                {
                    ret = ret + character.ToLower();
                }
                else
                {
                    if (character == " ")
                    {
                        toUppercase = true;
                    }
                    else if (toUppercase)
                    {
                        toUppercase = false;
                        ret = ret + character.ToUpper();
                    }
                    else
                    {
                        ret = ret + character;
                    }
                }
            }
            return ret;
        }
        #endregion

        #region email
        public static void sendMail(Process process, string toEmails, string subject, string body, string attachments = null, bool multiattach = false)
        {
            var message = new MailMessage();
            message.From = new MailAddress(ConfigurationManager.AppSettings["smtpUser"]);
            message.Subject = subject;
            message.Body = body;
            foreach (string toEmail in toEmails.Split(','))
                if(toEmail != "") message.To.Add(toEmail);
            
            if (attachments != null)
                if (!multiattach)
                    message.Attachments.Add(new System.Net.Mail.Attachment(attachments));
                else
                    foreach (string attachment in attachments.Split(','))
                        message.Attachments.Add(new System.Net.Mail.Attachment(attachment));

            SmtpClient smtp = new SmtpClient();
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;

            smtp.UseDefaultCredentials = false;
            smtp.Host = ConfigurationManager.AppSettings["smtpServer"];
            smtp.Port = Convert.ToInt32(ConfigurationManager.AppSettings["smtpPort"]);
            smtp.Credentials = new NetworkCredential(ConfigurationManager.AppSettings["smtpUser"], ConfigurationManager.AppSettings["smtpPassword"]);
            smtp.EnableSsl = true;
            try
            {
                smtp.Send(message);
            }
            catch (Exception ex)
            {
                process.log(ex.Message);
            }
            finally
            {
                message.Dispose();
            }
        }
        public static void sendReport(Process process, string toEmails, string subject, string body, string attachments = null, bool multiattach = false)
        {
            var message = new MailMessage();
            message.From = new MailAddress(ConfigurationManager.AppSettings["reportSmtpUser"]);
            message.Subject = subject;
            message.Body = body;
            foreach (string toEmail in toEmails.Split(','))
                if (toEmail != "") message.To.Add(toEmail);

            if (attachments != null)
                if (!multiattach)
                    message.Attachments.Add(new System.Net.Mail.Attachment(attachments));
                else
                    foreach (string attachment in attachments.Split(','))
                        message.Attachments.Add(new System.Net.Mail.Attachment(attachment));

            SmtpClient smtp = new SmtpClient();
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;

            smtp.UseDefaultCredentials = false;
            smtp.Host = ConfigurationManager.AppSettings["smtpServer"];
            smtp.Port = Convert.ToInt32(ConfigurationManager.AppSettings["smtpPort"]);
            smtp.Credentials = new NetworkCredential(ConfigurationManager.AppSettings["reportSmtpUser"], ConfigurationManager.AppSettings["reportSmtpPassword"]);
            smtp.EnableSsl = true;
            try
            {
                smtp.Send(message);
            }
            catch (Exception ex)
            {
                process.log(ex.Message);
            }
            finally
            {
                message.Dispose();
            }
        }
        public static void sendError(Process process, string content)
        {
            string subject = "ERROR in application DFPLoader";
            string body = content;

            var message = new MailMessage(ConfigurationManager.AppSettings["errorSmtpUser"], ConfigurationManager.AppSettings["toError"], subject, body);

            SmtpClient smtp = new SmtpClient();
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;

            smtp.UseDefaultCredentials = false;
            smtp.Host = ConfigurationManager.AppSettings["smtpServer"];
            smtp.Port = Convert.ToInt32(ConfigurationManager.AppSettings["smtpPort"]);
            smtp.Credentials = new NetworkCredential(ConfigurationManager.AppSettings["errorSmtpUser"], ConfigurationManager.AppSettings["errorSmtpPassword"]);
            smtp.EnableSsl = true;
            try
            {
                smtp.Send(message);
            }
            catch(Exception ex)
            {
                process.log(ex.Message);
            }
        }
        #endregion
    }
}
