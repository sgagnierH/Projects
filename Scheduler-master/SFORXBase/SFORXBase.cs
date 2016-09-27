using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SFORXBase.SFDC;
using Tc.TcMedia.Scheduler;
using MySql.Data.MySqlClient;

namespace Tc.TcMedia.SfoRx
{ 
    public class SFORXBase
    {
        private static LoginResult CurrentLoginResult = null;
        private static SforceService SfdcBinding = null;
        private static DateTime lastLogin = System.DateTime.MinValue;

        public static void Authenticate(Process process)
        {
            if (SfdcBinding != null && System.DateTime.Now.CompareTo(lastLogin.AddMinutes(110)) > 0) return;

            MySqlDataReader dataReader = Db.getMySqlReader(process, "Select * from 0_accounts where enabled=1 and system = 'sforx'");
            dataReader.Read();
            string username = dataReader.GetString("username");
            string password = dataReader.GetString("password");
            dataReader.Close();

            SfdcBinding = new SforceService();
            try
            {
                CurrentLoginResult = SfdcBinding.login(username, password);
                lastLogin = System.DateTime.Now;
            }
            catch (System.Web.Services.Protocols.SoapException e)
            {
                // This is likley to be caused by bad username or password
                SfdcBinding = null;
                throw (e);
            }
            catch (Exception e)
            {
                // This is something else, probably comminication
                SfdcBinding = null;
                throw (e);
            }

            //Change the binding to the new endpoint
            SfdcBinding.Url = CurrentLoginResult.serverUrl;

            //Create a new session header object and set the session id to that returned by the login
            SfdcBinding.SessionHeaderValue = new SessionHeader();
            SfdcBinding.SessionHeaderValue.sessionId = CurrentLoginResult.sessionId;
        }
        #region check
        public static void checkSfoObject(Process process, string tableName, string keyfield, Dictionary<string, string> obj)
        {
            bool added = false;
            MySqlConnection conn = Db.getConnection();
            MySqlDataReader dataReader = null;
            try
            {
                // Check if sfo_orderId exists in apnsf_orders
                dataReader = Db.getMySqlReader(process, "Select * from " + tableName + " where " + keyfield + " ='" + obj[keyfield] + "'");
                if (!dataReader.Read())
                {
                    dataReader.Close();
                    conn.Close();

                    conn.Open();
                    Db.execSqlCommand(process, "INSERT into " + tableName + " (" + keyfield + ") VALUES ('" + obj[keyfield] + "')");
                    process.log("New sfo Order");

                    // Refresh
                    dataReader = Db.getMySqlReader(process, "Select * from " + tableName + " where " + keyfield + " ='" + obj[keyfield] + "'");
                    dataReader.Read();
                    Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValueString,Info,Data) VALUES('" + keyfield + "','" + obj[keyfield] + "','Add','')");
                    added = true;
                }
                processCheck(process, obj, dataReader, tableName, keyfield, obj[keyfield], added);
                dataReader.Close();
            }
            catch (Exception ex)
            {
                process.log("Error comparing SFORX obects");
                throw new Exception("Error comparing SFORX objects", ex);
            }
            finally
            {
                if (!dataReader.IsClosed) dataReader.Close();
                conn.Close();
            }
        }
        public static void processCheck(Process process, Dictionary<string, string> obj, MySqlDataReader dataReader, string tableName, string keyField, string valueField, bool adding)
        /***
         * Scrolls through the database table and compare fields with existing in the object for a change
         **/
        {
            try
            {
                string strSql = keyField + "='" + valueField + "'";
                dynamic value = null;
                string property = null;

                for (int i = 1; i < dataReader.FieldCount; i++)
                {
                    string name = dataReader.GetName(i);

                    string type = dataReader.GetDataTypeName(i).ToString();

                    if (obj.ContainsKey(name))
                    {
                        value = obj[name];
                        property = "String";
                    }
                    else
                    {
                        value = null;
                        property = null;
                    }


                    if (property != null)
                    {
                        if (dataReader[name].ToString() != "" || value != null)
                        {
                            if (dataReader[name].ToString() != "" && value == null)
                            {
                                strSql += "," + name + "=null";
                            }
                            else
                            {
                                bool sfoValueIsNull = (value == null) ? true : false;
                                bool dbValueIsNull = false;
                                try
                                {
                                    if (dataReader.IsDBNull(i)) dbValueIsNull = true;
                                }
                                catch (Exception)
                                {
                                    dbValueIsNull = true;
                                }

                                if (sfoValueIsNull && dbValueIsNull)
                                {
                                    Console.Write("Nothing to do");
                                }
                                else
                                {
                                    switch (type)
                                    {
                                        case "DATETIME":
                                            if (dbValueIsNull)
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
                                            if (!dataReader.GetValue(i).Equals(Convert.ToInt64(value)))
                                                strSql += "," + name + "=" + value;
                                            break;
                                    }
                                }
                            }
                        }
                    }
                }
                if (strSql != keyField + "='" + valueField + "'")
                {
                    int nbchanges = 0;
                    string changes = "";
                    string[] sParam = strSql.Split(',');
                    for (int i = 1; i < sParam.Length; i++) // skip [0], it's keyfield, always there;
                    {
                        string[] sOpt = sParam[i].Split('=');
                        switch (sOpt[0])
                        {
                            // Don't want to log changes if only these fields have been modified
                            case "lastModifiedDateTime":
                                break;
                            default:
                                changes = changes + "," + sParam[i];
                                nbchanges++;
                                break;
                        }
                    }

                    if (nbchanges > 0)
                    {
                        if (!adding)
                        {
                            process.log(" ... Modified");
                            Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValueString,Info,Data) VALUES('" + keyField + "','" + valueField + "','Update','" + strSql.Replace("'", "''") + "')");
                        }
                        Db.execSqlCommand(process, "UPDATE " + tableName + " SET " + strSql + " WHERE " + keyField + "='" + valueField + "'");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Problème SFORXBase.processCheck", ex);
            }
        }
        #endregion

        #region SOQL
        public static QueryResult getSOQL(Process process, string soql)
        {
            QueryResult qr = null;
            try
            {
                qr = SfdcBinding.query(soql);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw (e);
            }
            return qr;
        }
        #endregion
    }
}
