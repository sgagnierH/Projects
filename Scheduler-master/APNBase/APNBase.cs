using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tc.TcMedia.Scheduler;
using MySql.Data.MySqlClient;
using System.Data.SqlClient;

namespace Tc.TcMedia.Apn
{
    public class APNApiException : Exception
    {
        public APNApiException() : base() { }
        public APNApiException(string message) : base(message) { }
        public APNApiException(string format, params object[] args) : base(string.Format(format, args)) { }
        public APNApiException(string message, Exception innerException): base(message, innerException) { }
        public APNApiException(string format, Exception innerException, params object[] args): base(string.Format(format, args), innerException) { }
        //protected APNApiException(SerializationInfo info, StreamingContext context): base(info, context) { }
    }
    public class APNBase
    {
        private static DateTime lastRun = new DateTime(2000,1,1,0,0,0);
        private static Encoding iso = Encoding.GetEncoding("ISO-8859-1");
        private static Encoding utf8 = Encoding.UTF8;

        #region check
        public static void checkApnObject(Process process, string tableName, string keyfield, dynamic obj)
        {
            bool added = false;
            MySqlConnection conn = Db.getConnection();
            MySqlDataReader dataReader = null;
            try
            {
                dataReader = Db.getMySqlReader(process, "Select * from " + tableName + " where " + keyfield + " = " + obj.id);
                if (!dataReader.Read())
                {
                    dataReader.Close();
                    conn.Close();

                    conn.Open();
                    Db.execSqlCommand(process, "INSERT into " + tableName + " (" + keyfield + ") VALUES (" + obj.id + ")");
                    Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValue,Info,Data) VALUES('" + keyfield + "'," + obj.id + ",'Add','')");

                    // Refresh
                    dataReader = Db.getMySqlReader(process, "Select * from " + tableName + " where " + keyfield + " = " + obj.id);
                    dataReader.Read();
                    added = true;
                }
                processCheck(process, obj, dataReader, tableName, keyfield, (long)obj.id, added);
            }
            catch (Exception ex)
            {
                process.log("Error comparing APN opbects");
                throw new Exception("Error comparing APN objects", ex);
            }
            finally
            {
                dataReader.Close();
                conn.Close();
            }
        }
        public static void processCheck(Process process, dynamic obj, MySqlDataReader dataReader, string tableName, string keyField, long valueField, bool adding)
        {
            processCheck(process, obj, dataReader, tableName, keyField, valueField.ToString(), adding);
        }
        public static void processCheck(Process process, dynamic obj, MySqlDataReader dataReader, string tableName, string keyField, string valueField, bool adding)
        /***
         * Scrolls through the database table and compare fields with existing in the object for a change
         **/
        {
            try
            {
                string strSql = keyField + "=" + valueField;
                dynamic value = null;
                Newtonsoft.Json.Linq.JValue property = null;

                for (int i = 1; i < dataReader.FieldCount; i++)
                {
                    string name = dataReader.GetName(i);
                    bool isList = false;

                    if (name != "seat")
                    {
                        string type = dataReader.GetDataTypeName(i).ToString();

                        switch(name)
                        {
                            case "filtered_advertisers":
                            case "filtered_line_items":
                            case "filtered_campaigns":
                            case "inventory_attributes":
                            case "private_sizes":
                                //isList = true;
                                //value = (String)obj.filtered_advertisers.
                                break;
                            default:
                                property = obj[name];
                                value = (property == null) ? null : property.Value;
                                break;
                        }
                        

                        if (property != null || isList)
                        {
                            if (dataReader[name].ToString() != "" || value != null)
                            {
                                if (dataReader[name].ToString() != "" && value == null)
                                {
                                    strSql += "," + name + "=null";
                                }
                                else
                                {
                                    bool apnValueIsNull = (value == null) ? true : false;
                                    bool dbValueIsNull = false;
                                    try
                                    {
                                        if (dataReader.IsDBNull(i)) dbValueIsNull = true;
                                    } 
                                    catch(Exception)
                                    {
                                        dbValueIsNull = true;
                                    }

                                    if (apnValueIsNull && dbValueIsNull)
                                    {
                                        // Nothing to do
                                        int k = 1;
                                    }
                                    else
                                    {
                                        switch (type)
                                        {
                                            case "DATETIME":
                                                if(dbValueIsNull)
                                                    strSql += "," + name + "='" + value + "'";
                                                else if (dataReader.GetDateTime(i).ToString("yyyy-MM-dd HH:mm:ss").CompareTo(value.ToString()) != 0)
                                                    strSql += "," + name + "='" + value + "'";
                                                break;

                                            case "VARCHAR":
                                                if (dbValueIsNull)
                                                    strSql += "," + name + "='" + ((String)value).Replace("'", "''") + "'";
                                                else if (dataReader.GetValue(i).ToString().CompareTo(value.ToString()) != 0)
                                                    strSql += "," + name + "='" + ((String)value).Replace("'", "''") + "'";
                                                break;

                                            case "BIT":
                                                if ((Convert.ToInt16(dataReader[name]) == 1) != value)
                                                    if (value)
                                                        strSql += "," + name + "=1";
                                                    else
                                                        strSql += "," + name + "=0";
                                                break;

                                            default:
                                                if (!dataReader.GetValue(i).Equals(value))
                                                    strSql += "," + name + "=" + value;
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (strSql != keyField + "=" + valueField)
                {
                    if (!adding)
                    {
                        process.log(" ... Modified");
                        Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValue,Info,Data) VALUES('" + keyField + "'," + valueField + ",'Update','" + strSql.Replace("'", "''") + "')");
                    }
                    Db.execSqlCommand(process, "UPDATE " + tableName + " SET " + strSql + " WHERE " + keyField + "=" + valueField);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Problème APNBase.processCheck", ex);
            }
        }

        public static string toMySqlDate(string inDate)
        {
            return inDate;
        }
        public static bool DifferentDate(System.DateTime mySqlDateTime, string value)
        {
            bool ret = false;
            if (mySqlDateTime.Year.ToString() != value.Substring(0,4)) ret = true;
            if (mySqlDateTime.Month.ToString() != value.Substring(5,2)) ret = true;
            if (mySqlDateTime.Day.ToString() != value.Substring(8,2)) ret = true;
            if (mySqlDateTime.Hour.ToString() != value.Substring(11,2)) ret = true;
            if (mySqlDateTime.Minute.ToString() != value.Substring(14,2)) ret = true;
            if (mySqlDateTime.Second.ToString() != value.Substring(17,2)) ret = true;
            return ret;
        }
        #endregion

        public static dynamic callApi(Process process, APNAuth auth, string param = "", string stringToPost = null, string saveFilePath = null)
        {
            int apiDelay = 2000;
            TimeSpan offset = DateTime.Now.Subtract(lastRun);
            if (offset.TotalMilliseconds < apiDelay)
                System.Threading.Thread.Sleep(Convert.ToInt16(apiDelay - offset.TotalMilliseconds));
            lastRun = DateTime.Now;

            dynamic ret = null;
            Stream requestStream = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(APNAuth.apiUrl + param);
                request.CookieContainer = new CookieContainer();
                request.CookieContainer.Add(auth.cookie);

                if (stringToPost == null)
                {
                    request.Method = "GET";
                }
                else
                {
                    byte[] data = Encoding.ASCII.GetBytes(stringToPost);
                    request.Method = "POST";
                    request.Timeout = 30000;
                    request.ContentType = "application/x-www-form-urlencoded";
                    requestStream = request.GetRequestStream();
                    requestStream.Write(data, 0, data.Length);
                    requestStream.Close();
                }

                int success = 0;
                int retries = 0;
                while (success == 0 && retries < 5)
                {
                    try
                    {
                        request.Timeout = 30000000;
                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        if (response.StatusCode.ToString() == "OK")
                        {
                            Stream responseStream = response.GetResponseStream();
                            if (saveFilePath == null)
                            {
                                StreamReader myStreamReader = new StreamReader(responseStream, Encoding.Default);
                                string tmp = myStreamReader.ReadToEnd();
                                dynamic dynObj = JsonConvert.DeserializeObject(tmp);
                                if (dynObj.response.status == "OK")
                                    ret = dynObj.response;

                                myStreamReader.Close();
                            }
                            else
                            {
                                if (File.Exists(saveFilePath))
                                    File.Delete(saveFilePath);

                                using (responseStream)
                                {
                                    using (Stream fileStream = File.OpenWrite(saveFilePath))
                                    {
                                        byte[] buffer = new byte[4096];
                                        int bytesRead = responseStream.Read(buffer, 0, 4096);
                                        while (bytesRead > 0)
                                        {
                                            fileStream.Write(buffer, 0, bytesRead);
                                            bytesRead = responseStream.Read(buffer, 0, 4096);
                                        }
                                    }
                                }
                            }
                            responseStream.Close();
                        }
                        success = 1;
                    }
                    catch (Exception ex)
                    {
                        retries++;

                        process.log("Retrying " + retries + " - " + ex.Message);
                        System.Threading.Thread.Sleep(6000); // Don't flood Appnexus's API

                        if (retries == 5)
                            throw new APNApiException("Unable to reach AppNexus's API", ex);
                    }
                }
            }
            catch(Exception ex)
            {
                throw new APNApiException("Unable to reach AppNexus's API", ex);
            }
            return ret;
        }
        public static void waitForReport(Process process, APNAuth auth,string reportId, string filePath)
        {
            dynamic ret = null;
            process.log("Waiting for report to be ready");

            do
            {
                ret = callApi(process, auth, "report?name=Redux&id=" + reportId);
                if (ret.execution_status == "error") {
                    var ex = new APNApiException("Report Failed " + ret.error);
                    throw ex;
                }
            } while (ret.execution_status != "ready");

            process.log("Downloading report");
            callApi(process, auth, "report-download?id=" + reportId, null, filePath);

            process.log("Report downloaded");
        }
        public static dynamic getPlacementById(Process process, APNAuth auth, long appnexusId)
        {
            dynamic response = callApi(process, auth, "placement?id=" + appnexusId);
            return (response == null) ? null : response.placement;
        }
        public static SeatConfig getSeats(Process process)
        {
            SeatConfig ret = new SeatConfig();
            ret.seats = new List<Seat>();

            //string SQL = "Select * FROM apn_seats order by no DESC";
            string SQL = "Select no, username, password FROM 0_accounts WHERE system='apn' AND enabled=1";
            MySqlConnection con2 = new MySqlConnection(Db.connectionString);
            MySqlDataReader dataReader = Db.getMySqlReader(process, SQL, con2);

            while(dataReader.Read())
            {
                Seat seat = new Seat();
                seat.no = dataReader.GetInt16("no");
                seat.username = dataReader.GetString("username");
                seat.password = dataReader.GetString("password");
                ret.seats.Add(seat);
            }
            dataReader.Close();
            con2.Close();
            return ret;
        }

        #region MSSql
        public static void getFinishedLineItems(Process process, System.DateTime date)
        {
            SqlConnection msSqlCon = new SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings["dfp"].ConnectionString);
            SqlDataReader dataReader = null;

            string sql = "select description, start_date, end_date, optionid, product_id, tracking_number, client_id, sales_rep, pubid, impressions, click_url, newspaper_publication_id, active, sales_rep_name ";
            sql += "from sfdc_opportunity_line_item ";
            sql += "where end_date = '" + date.ToString("yyyy-MM-dd") + "' ";
            sql += "order by tracking_number asc, start_date asc ";

            try
            {
                msSqlCon.Open();
                SqlCommand command = new SqlCommand(sql, msSqlCon);

                dataReader = command.ExecuteReader();
                string addSql = "";

                while(dataReader.Read())
                {
                    addSql += "INSERT INTO apn_finished (description, start_date, end_date, optionid, ";
                    addSql += "product_id, tracking_number, client_id, sales_rep, ";
                    addSql += "pubid, impressions, click_url, newspaper_publication_id, active, ";
                    addSql += "sales_rep_name) ";
                    addSql += "VALUES ('" + dataReader.GetString(0) + "','" + Db.toMySqlDate(dataReader.GetDateTime(1)) + "','" + Db.toMySqlDate(dataReader.GetDateTime(2)) + "','" + dataReader.GetString(3) + "',";
                    addSql += "'" + dataReader.GetString(4) + "'," + dataReader.GetString(5) + ",'" + dataReader.GetString(6) + "','" + dataReader.GetString(7) + "',";
                    addSql += "'" + dataReader.GetString(8) + "'," + dataReader.GetInt64(9) + ",'" + dataReader.GetString(10) + "'," + dataReader.GetInt64(11) + "," + dataReader.GetBoolean(12) + ",";
                    addSql += "'" + dataReader.GetString(12) + "');\n";
                }

                Db.execSqlCommand(process, "DELETE FROM apn_finished WHERE end_date = '" + date.ToString("yyyy-MM-dd") + "'");
                Db.execSqlCommand(process, addSql);
            }
            catch(Exception ex)
            {
                throw new Exception("Unable to connect to MSSQL", ex);
            }
            finally
            {
                msSqlCon.Close();
            }
        }
        #endregion
    }
    public class SeatConfig
    {
        public List<Seat> seats { get; set; }
    }
    public class Seat
    {
        public int no { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public APNAuth auth { get; set; }
        public Dictionary<long, long> placementIds = new Dictionary<long, long>();
    }
}
