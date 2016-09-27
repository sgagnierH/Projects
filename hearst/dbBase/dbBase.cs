#define MYSQL
using System;
using System.Reflection;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201605;
using Google.Api.Ads.Dfp.v201605;
using Google.Api.Ads.Common.Util;
using System.Data.SqlClient;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace com.hearst.db
{
    public class dbBase
    {
        public static string connectionString = "Server=127.0.0.1; Uid=sgagnier; database=adops; password=ur2i4me";
        public const string TIME_FORMAT = "hh:mm:ss";
        public const string DATETIME_FORMAT = "yyyyMMdd";
        public const string DATETIME_FORMAT_FULL = "yyyyMMddhhmmss";
        public static string guid = Guid.NewGuid().ToString();
        public static MySqlConnection conn = null;

        #region mssql
#if (MSSQL)
        public static string getLastModifiedDateTime(string table, int days = 0)
        {
            string ret = "";
            SqlConnection conn = getConnection();
            SqlDataReader dataReader = null;
            try
            {
                dataReader = getSqlReader("SELECT MAX(lastModifiedDateTime) AS lastModifiedDateTime FROM " + table);
                if (dataReader.Read())
                    ret = toSqlDate(((System.DateTime)(dataReader["lastModifiedDateTime"])).AddDays(-days)).Replace(" ", "T");
            }
            catch (Exception)
            { }
            finally
            {
                if(dataReader != null)
                    dataReader.Close();
                conn.Close();
            }
            if (ret == "0001-01-01 00:00:00" || ret == "2000-01-01 00:00:00")
                ret = "";
            return ret;
        }
        public static string toSqlDate(System.DateTime date)
        {
            string ret = "";

            try
            {
                ret = date.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (Exception) { }

            return ret;
        }
        public static void execSqlCommand(string strSql, SqlConnection altConn = null)
        {
            SqlCommand cmd = new SqlCommand();
            cmd.Connection = getConnection();
            cmd.CommandTimeout = 3000;
            cmd.CommandText = strSql;

            if (altConn != null)
                cmd.Connection = altConn;

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
                        log(ex.Message);
                        log("---> " + strSql);
                        throw new Exception("Error executing Sql statement", ex);
                    }
                }
            }
        }
        public static SqlDataReader getSqlReader(string strSql, SqlConnection altConn = null)
        {
            SqlDataReader dataReader = null;
            SqlCommand command = new SqlCommand();
            command.Connection = getConnection();
            command.CommandText = strSql;
            command.CommandTimeout = 5000;

            if (altConn != null)
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
                        log(ex.Message);
                        throw new Exception("Error getting Reader", ex);
                    }
                }
            }
            return dataReader;
        }
        public static SqlConnection getConnection()
        {
            if(connectionString == null)
                connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["com.hearst.Properties.Settings.AdOpsDFPConnectionString"].ConnectionString;

            return new SqlConnection(connectionString);
        }
        public static string getFieldName(string dfpName)
        {
            string ret = "";
            bool toUppercase = false;
            for (int i = 0; i < dfpName.Length; i++)
            {
                string character = dfpName.Substring(i, 1).Replace("%", "Pct");
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
#endif
        #endregion

        #region mysql
#if (MYSQL)
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
        public static string getLastModifiedDateTime(string table, int days = 0)
        {
            string ret = toMySqlDate(System.DateTime.MinValue);
            MySqlDataReader dataReader = null;
            try
            {
                dataReader = getSqlReader("SELECT MAX(lastModifiedDateTime) AS lastModifiedDateTime FROM " + table);
                if (dataReader.Read())
                    ret = toMySqlDate(((System.DateTime)(dataReader["lastModifiedDateTime"])).AddDays(-days)).Replace(" ", "T");
            }
            catch (Exception)
            { }
            finally
            {
                if (dataReader != null)
                    dataReader.Close();
            }
            if (ret == "0001-01-01 00:00:00" || ret == "0001-01-01T00:00:00" || ret == "2000-01-01T00:00:00")
                ret = "";
            return ret;
        }
        public static void execSqlCommand(string strSql, MySqlConnection altConn = null)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = getConnection();
            cmd.CommandTimeout = 3000;
            cmd.CommandText = strSql;

            if (altConn != null)
                cmd.Connection = altConn;

            int success = 0;
            int retries = 0;
            while (success == 0 && retries < 5)
            {
                try
                {
                    if (cmd.Connection.State != System.Data.ConnectionState.Open)
                        cmd.Connection.Open();
                    cmd.ExecuteNonQuery();
                    success = 1;
                }
                catch (Exception ex)
                {
                    retries++;
                    System.Threading.Thread.Sleep(100);
                    if (retries == 5)
                    {
                        dbBase.log(ex.Message);
                        dbBase.log("---> " + strSql);
                        throw new Exception("Error executing MySql statement", ex);
                    }
                }
            }
        }
        public static MySqlDataReader getSqlReader(string strSql, MySqlConnection altConn = null)
        {
            MySqlDataReader dataReader = null;
            MySqlCommand command = new MySqlCommand();
            command.Connection = getConnection();
            command.CommandText = strSql;
            command.CommandTimeout = 5000;

            if (altConn != null)
                command.Connection = altConn;

            int success = 0;
            int retries = 0;
            while (success == 0 && retries < 5)
            {
                try
                {
                    if (command.Connection.State != System.Data.ConnectionState.Open)
                        command.Connection.Open();
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
                        dbBase.log(ex.Message);
                        throw new Exception("Error getting Reader", ex);
                    }
                }
            }
            return dataReader;
        }
        public static MySqlConnection getConnection()
        {
            if (conn == null)
                conn = new MySqlConnection(connectionString);
            return conn;
        }
        public static MySqlConnection getNewConnection()
        {
            return new MySqlConnection(connectionString);
        }
        public static string getFieldName(string dfpName)
        {
            string ret = "";
            bool toUppercase = false;
            for (int i = 0; i < dfpName.Length; i++)
            {
                string character = dfpName.Substring(i, 1).Replace("%", "Pct");
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
#endif
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
        public static string MakeValidFileName(string name)
        {
            string res = Regex.Replace(name, @"[^\p{L}\p{N}\s\-]+", "");
            return res.Replace(" ", "_");
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
        public static void log(string text)
        {
            try
            {
                Console.WriteLine(text);
            }
            catch (Exception) { }
        }
#endregion

        #region email
        public static void sendMail(string toEmails, string subject, string body, string attachments = null, bool multiattach = false)
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
                log(ex.Message);
            }
            finally
            {
                message.Dispose();
            }
        }
        public static void sendError(string content)
        {
            //string subject = "ERROR in application DFPLoader";
            //string body = content;

            //var message = new MailMessage(ConfigurationManager.AppSettings["errorSmtpUser"], ConfigurationManager.AppSettings["toError"], subject, body);

            //SmtpClient smtp = new SmtpClient();
            //smtp.DeliveryMethod = SmtpDeliveryMethod.Network;

            //smtp.UseDefaultCredentials = false;
            //smtp.Host = ConfigurationManager.AppSettings["smtpServer"];
            //smtp.Port = Convert.ToInt32(ConfigurationManager.AppSettings["smtpPort"]);
            //smtp.Credentials = new NetworkCredential(ConfigurationManager.AppSettings["errorSmtpUser"], ConfigurationManager.AppSettings["errorSmtpPassword"]);
            //smtp.EnableSsl = true;
            //try
            //{
            //    smtp.Send(message);
            //}
            //catch(Exception ex)
            //{
            //    log(ex.Message);
            //}
            log(content);
        }
#endregion
    }
}
