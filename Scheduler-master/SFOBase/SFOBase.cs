using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SFOBase.SFDC;
using Tc.TcMedia.Scheduler;
using MySql.Data.MySqlClient;

namespace Tc.TcMedia.Sfo
{ 
    public class SFOBase
    {
        private static LoginResult CurrentLoginResult = null;
        private static SforceService SfdcBinding = null;
        private static DateTime lastLogin = System.DateTime.MinValue;

        public static void Authenticate(Process process)
        {
            if (SfdcBinding != null && System.DateTime.Now.CompareTo(lastLogin.AddMinutes(110)) > 0) return;

            MySqlDataReader dataReader = Db.getMySqlReader(process, "Select * from 0_accounts where enabled=1 and system = 'sfo'");
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
        public static void checkSfoObject(Process process, string tableName, string keyfield, Dictionary<string,string>obj)
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

                // Must establish link between our info and SalesForce when it's Orders
                if (obj.ContainsKey("orderName"))
                {
                    // check if relation between sfo_orderId and AdTrackingNo exists
                    dataReader = Db.getMySqlReader(process, "Select * from sfo_orders_adtrackingno where adTrackingNo=" + obj["adTrackingNo"] + " AND sfo_orderId='" + obj["sfo_orderId"] + "'");
                    if (!dataReader.Read())
                    {
                        Db.execSqlCommand(process, "INSERT into sfo_orders_adtrackingno (adTrackingNo,sfo_orderId) VALUES (" + obj["adTrackingNo"] + ",'" + obj["sfo_orderId"] + "')");
                        process.log("New relation");
                    }
                    dataReader.Close();

                    // check if relation between sfo_orderId and orderlineNo exists
                    dataReader = Db.getMySqlReader(process, "Select * from sfo_orders_orderlineno where sfo_orderlineNo='" + obj["sfo_orderlineNo"] + "' AND sfo_orderId='" + obj["sfo_orderId"] + "'");
                    if (!dataReader.Read())
                    {
                        Db.execSqlCommand(process, "INSERT into sfo_orders_orderlineno (sfo_orderlineNo,sfo_orderId) VALUES ('" + obj["sfo_orderlineNo"] + "','" + obj["sfo_orderId"] + "')");
                        process.log("New relation");
                    }
                    dataReader.Close();
                }
            }
            catch (Exception ex)
            {
                process.log("Error comparing SFP opbects");
                throw new Exception("Error comparing SFO objects", ex);
            }
            finally
            {
                if(!dataReader.IsClosed) dataReader.Close();
                conn.Close();
            }
        }
        public static void processCheck(Process process, Dictionary<string,string>obj, MySqlDataReader dataReader, string tableName, string keyField, string valueField, bool adding)
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
                throw new Exception("Problème SFOBase.processCheck", ex);
            }
        }

        #endregion

        public static void getOrderInfo(Process process, string AdNumbers, string lastModifiedDateTime = "")
        {
            Authenticate(process);

            QueryResult queryResult = null;
            string SOQL = "";
            if(lastModifiedDateTime == "")
                lastModifiedDateTime = Db.getLastModifiedDateTime(process, "sfo_orders") + "Z";

            try
            {
                SOQL = "SELECT Name, OrderName__c, Additional_Information_Newspaper__r.Ad_Number__c, Order__r.Id, Order__r.Name, Order__r.PoNumber__c, Order__r.Account__r.Name, Order__r.Account__r.Language__c, Order__r.SalesRep__r.Name, Order__r.SalesRep__r.Email, LastModifiedDate FROM Order_Line__c WHERE " + ((lastModifiedDateTime == "") ? "" : " LastModifiedDate > " + lastModifiedDateTime + " AND ") + " Additional_Information_Newspaper__r.Ad_Number__c ";
                if (AdNumbers == null)
                    SOQL += "!= Null ORDER BY Additional_Information_Newspaper__r.Ad_Number__c DESC";
                else
                    SOQL += "in (" + AdNumbers + ")";

                process.log("Getting Orders from SalesForce" + ((lastModifiedDateTime == "") ? "" : " since " + lastModifiedDateTime));

                queryResult = SfdcBinding.query(SOQL);
                long nbRecords = 0;
                while(queryResult.records.Count() > 0)
                {
                    for (int i = 0; i < queryResult.records.Count(); i++)
                    {
                        Order_Line__c ret = (Order_Line__c)queryResult.records[i];

                        Dictionary<string, string> order = new Dictionary<string, string>();
                        order.Add("sfo_orderlineNo", ret.Name);
                        order.Add("adTrackingNo", ret.Additional_Information_Newspaper__r.Ad_Number__c);
                        order.Add("sfo_orderId", ret.Order__r.Id);
                        order.Add("orderName", (ret.OrderName__c == null) ? ret.Order__r.Name : ret.OrderName__c);
                        order.Add("orderNo", ret.Order__r.Name);
                        order.Add("poNumber", ret.Order__r.PoNumber__c);
                        order.Add("accountName", ret.Order__r.Account__r.Name);
                        order.Add("salesRep", ret.Order__r.SalesRep__r.Name);
                        order.Add("salesRepEmail", ret.Order__r.SalesRep__r.Email);
                        order.Add("lastModifiedDateTime", Db.toMySqlDate((DateTime)ret.LastModifiedDate));
                        order.Add("accountLanguage", (ret.Order__r.Account__r.Language__c == null) ? "en" : ret.Order__r.Account__r.Language__c.ToLower().Substring(0,2));

                        //string notes = "";
                        //string notesql = "SELECT Body FROM Note WHERE ParentId='" + ret.Order__r.Id + "'";
                        //QueryResult queryResultNotes = SfdcBinding.query(notesql);
                        //if(queryResultNotes.size > 0)
                        //    for (int j = 0; j < queryResultNotes.records.Count(); j++)
                        //        notes += ((Note)queryResultNotes.records[j]).Body + "\n";
                        //order.Add("notes",notes);

                        process.log(++nbRecords + "/" + queryResult.size + " - " + ret.Additional_Information_Newspaper__r.Ad_Number__c + " " + order["orderName"]);
                        checkSfoObject(process, "sfo_orders", "sfo_orderId", order);
                        order.Clear();
                    }

                    if (!queryResult.done)
                    {
                        process.log("Getting more Orders from SalesForce");
                        queryResult = SfdcBinding.queryMore(queryResult.queryLocator);
                    }
                    else
                        break;
                } 
            }
            catch(Exception ex)
            {
                SfdcBinding = null;
                Console.Write("error");
                throw(ex);
            }
        }
        public static void OrderAddAttachement(Process process, string orderId, string filename)
        {
            Authenticate(process);

            QueryResult queryResult = null;
            string SOQL = "";

            try
            {
                if (!File.Exists(filename))
                {
                    process.log("File doesn not exist: " + filename);
                    throw new FileNotFoundException(filename);
                }

                FileInfo fi = new FileInfo(filename);

                SOQL = "SELECT id, (SELECT Name FROM Attachments) FROM Order__c WHERE id='" + orderId + "'";
                process.log("Getting Attachments to Order " + orderId + " from SalesForce");
                queryResult = SfdcBinding.query(SOQL);

                Attachment attachment = null;
                bool found = false;
                //Checking if not already exists
                for (int i = 0; i < queryResult.records.Count(); i++)
                {
                    Order__c res = (Order__c)queryResult.records[i];
                    if (res.Attachments != null)
                    {
                        foreach (sObject obj in res.Attachments.records)
                        {
                            attachment = (Attachment)obj;
                            if (attachment.Name == fi.Name)
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                }

                if (!found)
                {
                    attachment = new Attachment();
                    attachment.ParentId = orderId; // Attach attachement to opportunity.
                    attachment.ContentType = "application/pdf";
                    attachment.Name = fi.Name;
                }

                // Getting the actual content
                FileStream fs = fi.OpenRead();
                byte[] pdf_content = new byte[(int)fs.Length];
                int numBytesToRead = (int)fs.Length;
                int numBytesRead = 0;
                while (numBytesToRead > 0)
                {
                    int n = fs.Read(pdf_content, numBytesRead, numBytesToRead);
                    if (n == 0) break; // Break when the end of the file is reached. 
                    numBytesRead += n;
                    numBytesToRead -= n;
                }
                if (numBytesRead == 0)
                {
                    process.log("Could not read content of file");
                    throw new IOException("Could not read file " + filename);
                }
                attachment.Body = pdf_content;

                //updating
                SaveResult[] saveResults = null;
                if(!found)
                    saveResults = SfdcBinding.create(new sObject[] { attachment });
                else
                    saveResults = SfdcBinding.update(new sObject[] { attachment });
                if (saveResults[0].success)
                    Db.execSqlCommand(process, "UPDATE sfo_orders set attached=1 WHERE sfo_orderId like \"" + orderId + "\"");
                process.log(saveResults[0].success + " " + saveResults[0].errors);
            }
            catch (Exception ex)
            {
                SfdcBinding = null;
                throw (ex);
            }
        }
    }
}
