using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Serialization;
using com.hearst.db;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201605;
using Google.Api.Ads.Dfp.v201605;
using Google.Api.Ads.Common.Util;
using MySql.Data.MySqlClient;

namespace com.hearst.dfp
{
    public class dfpBase
    {
        public MySqlConnection conn = null;
        public static DfpUser dfpUser;
        public dfpBase()
        {

            dfpUser = com.hearst.utils.OAuth2.getDfpUser();
            conn = dbBase.getConnection();
        }

        public void getPages(dfpBase runner, string table, dynamic pageType, string keyField, string[] args, int offset = StatementBuilder.SUGGESTED_PAGE_LIMIT)
        {
            string lastModifiedDateTime = null;
            if (args.Length > 0)
                if (args[0].ToLower() == "all")
                    lastModifiedDateTime = "";

            if(lastModifiedDateTime == null)
                lastModifiedDateTime = dbBase.getLastModifiedDateTime(table);

            dbBase.log("Getting items" + ((lastModifiedDateTime == "") ? "" : " since " + lastModifiedDateTime));

            dynamic service = getService(dfpUser);

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("lastModifiedDateTime ASC")
                .Where((lastModifiedDateTime == "") ? "" : "lastModifiedDateTime > :lastModifiedDateTime")
                .AddValue("lastModifiedDateTime", lastModifiedDateTime)
                .Limit(offset);

            int i = 0;
            dynamic page = null;

            do
            {
                int success = 0;
                int retries = 0;
                while (success == 0 && retries < 5)
                {
                    try
                    {
                        dbBase.log("Loading...");
                        page = runner.getPage(service, statementBuilder, offset);
                        dbBase.log(" .. Loaded ");
                        success = 1;
                    }
                    catch (Exception ex)
                    {
                        retries++;
                        if (retries == 5)
                        {
                            dbBase.log("Retry " + retries);
                            dbBase.sendError(ex.Message);
                            throw new Exception("Unable to process");
                        }
                    }
                }

                if (page.results != null && page.results.Length > 0)
                {
                    foreach (dynamic item in page.results)
                    {
                        if (item.GetType().GetProperty("name") == null)
                            dbBase.log(i++ + "/" + page.totalResultSetSize);
                        else if (item.GetType().GetProperty("name") == null)
                            dbBase.log(i++ + "/" + page.totalResultSetSize + " : " + item.id);
                        else
                            dbBase.log(i++ + "/" + page.totalResultSetSize + " : " + item.id + " - " + item.name);
                        runCustomCode(item);
                        checkDfpObject(runner, table, keyField, item);
                    }
                }

                statementBuilder.IncreaseOffsetBy(offset);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            dbBase.log("Number of items found: " + page.totalResultSetSize);
        }

        #region virtual
        public virtual dynamic getService(DfpUser user)
        {
            return null;
        }
        public virtual dynamic getPage(dynamic service, StatementBuilder statementBuilder, int offset = StatementBuilder.SUGGESTED_PAGE_LIMIT)
        {
            return null;
        } 
        public virtual void runCustomCode(dynamic item)
        {
         
        }
        public virtual void processCustomField(dynamic item, dynamic property, dynamic dataReader, ref string strSql, ref bool fieldProcessed, ref int i)
        {
            string fieldName = dataReader.GetName(i);
            dynamic value = (property == null) ? null : property.GetValue(item, null);
        }
        public virtual dynamic getDeserialized(string xmlString)
        {
            return null;
        }
        #endregion

        #region check
        public static void checkDfpObject(dfpBase runner, string tableName, string keyfield, dynamic obj)
        {
            bool added = false;
            MySqlConnection conn = dbBase.getConnection();
            MySqlDataReader dataReader = null;
            int retries = 0;
            bool success = false;
            while (retries < 1 && !success)
            {
                try
                {
                    dataReader = dbBase.getSqlReader("Select * from " + tableName + " where " + keyfield + " = " + obj.id);
                    if (!dataReader.Read())
                    {
                        dataReader.Close();
                        dbBase.execSqlCommand("INSERT into " + tableName + " (" + keyfield + ") VALUES (" + obj.id + ")");

                        // Refresh
                        dataReader = dbBase.getSqlReader("Select * from " + tableName + " where " + keyfield + " = " + obj.id);
                        dataReader.Read();
                        added = true;
                    }
                    processCheck(runner, obj, dataReader, tableName, keyfield, obj.id, added);
                    success = true;
                }
                catch (Exception ex)
                {
                    if (retries++ == 5)
                    {
                        dbBase.log("Error comparing DFP objects");
                        throw new Exception("Error comparing DFP objects with " + keyfield + "=" + obj.id, ex);
                    }
                }
                finally
                {
                    if (dataReader != null)
                        if (!dataReader.IsClosed)
                            dataReader.Close();
                }
            }
        }
        public static void processCheck(dfpBase runner, dynamic obj, MySqlDataReader dataReader, string tableName, string keyField, long valueField, bool adding = false)
        {
            processCheck(runner, obj, dataReader, tableName, keyField, valueField.ToString(), adding);
        }
        public static void processCheck(dfpBase runner, dynamic obj, MySqlDataReader dataReader, string tableName, string keyField, string valueField, bool adding = false)
        /***
         * Scrolls through the database table and compare fields with existing in the object for a change
         **/
        {
            // var ass = typeof(dfpBase).Assembly;
            // var cul = new CultureInfo("fr-CA");
            // cul.NumberFormat.NumberDecimalSeparator = ".";
            // CultureInfo.DefaultThreadCurrentCulture = cul;

            string name = null;
            #region tableFields
            try
            {
                string strSql = keyField + "=" + valueField;
                dynamic value = null;
                PropertyInfo property = null;

                for (int i = 1; i < dataReader.FieldCount; i++)
                {
                    name = dataReader.GetName(i);
                    try
                    {
                        if (name != "deleted")
                        {

                            bool fieldProcessed = false;
                            value = null;
                            string type = dataReader.GetDataTypeName(i).ToString().ToUpper();
                            property = obj.GetType().GetProperty(name);

                            // Custom fields processing
                            runner.processCustomField(obj, property, dataReader, ref strSql, ref fieldProcessed, ref i);

                            if (property != null)
                            {
                                #region subObject
                                if (obj.GetType().GetProperty(name) == null && name.IndexOf('_') > 1)
                                {
                                    string[] tmpName = name.Split('_');
                                    if (obj.GetType().GetProperty(tmpName[0]) != null)
                                        property = obj.GetType().GetProperty(tmpName[0]);
                                }
                                #endregion

                                value = property.GetValue(obj, null);
                                string dfpType = (value == null) ? null : value.GetType().Name;

                                // Global fields
                                #region size
                                if (property.PropertyType.Name == "Size")
                                {
                                    if (value == null)
                                    {
                                        if ((string)dataReader["size_isAspectRatio"].ToString() != "0")
                                            strSql += ",size_isAspectRatio=0";

                                        if ((string)dataReader["size_isAspectRatio"].ToString() != "0")
                                            strSql += ",size_isAspectRatio=0";

                                        if ((string)dataReader["size_width"].ToString() != "0")
                                            strSql += ",size_width=0";
                                    }
                                    else
                                    {
                                        if (dataReader.IsDBNull(i))
                                            strSql += ",size_isAspectRatio=" + ((((Google.Api.Ads.Dfp.v201605.Size)value).isAspectRatio) ? 1 : 0);
                                        else if (Convert.ToBoolean(dataReader.GetValue(i)) != ((Google.Api.Ads.Dfp.v201605.Size)value).isAspectRatio)
                                            strSql += ",size_isAspectRatio=" + ((((Google.Api.Ads.Dfp.v201605.Size)value).isAspectRatio) ? 1 : 0);

                                        if (dataReader.IsDBNull(i + 1))
                                            strSql += ",size_height=" + ((Google.Api.Ads.Dfp.v201605.Size)value).height;
                                        else if (Convert.ToInt32(dataReader["size_height"]) != ((Google.Api.Ads.Dfp.v201605.Size)value).height)
                                            strSql += ",size_height=" + ((Google.Api.Ads.Dfp.v201605.Size)value).height;

                                        if (dataReader.IsDBNull(i + 2))
                                            strSql += ",size_width=" + ((Google.Api.Ads.Dfp.v201605.Size)value).width;
                                        else if (Convert.ToInt32(dataReader["size_width"]) != ((Google.Api.Ads.Dfp.v201605.Size)value).width)
                                            strSql += ",size_width=" + ((Google.Api.Ads.Dfp.v201605.Size)value).width;
                                    }
                                    i = i + 2;
                                }
                                #endregion


                                // Standards fields
                                if (!fieldProcessed)
                                {
                                    #region Basic
                                    bool dfpValueIsNull = (value == null) ? true : false;
                                    bool dbValueIsNull = (dataReader.GetType().GetProperty(name) == null) ? true : false;

                                    // when a field with "Specified is present, if set to false, value is NULL
                                    if (obj.GetType().GetProperty(name + "Specified") != null)
                                    {
                                        PropertyInfo specifiedProperty = obj.GetType().GetProperty(name + "Specified");
                                        //dfpValueIsNull = !obj.GetType().GetProperty(name + "Specified").GetValue(obj, null);
                                    }

                                    if (dfpValueIsNull && dbValueIsNull)
                                    {
                                        // Nothing to do
                                        int k = 1; k++; // No warning
                                    }
                                    else if (property.PropertyType.Name == "Int64[]")
                                    {
                                        string ids = "";
                                        foreach (long id in value)
                                            ids = ids + ((ids == "") ? "" : ",") + id;

                                        if (dataReader[name].ToString() != ids)
                                            strSql += "," + name + "='" + ids + "'";
                                    }
                                    else if (type == "DATETIME" || type == "DATETIME2")
                                    {
                                        System.DateTime dbDate = System.DateTime.MinValue;
                                        try
                                        {
                                            dbDate = System.DateTime.Parse(dataReader[name].ToString());
                                        }
                                        catch (Exception) { }

                                        if (value == null)
                                        {
                                            strSql += "," + name + "=null";
                                            try
                                            {
                                                if (dataReader[name + "timeZoneID"] != null)
                                                    strSql += "," + name + "timeZoneID=null";
                                            }
                                            catch (Exception) { }
                                        }
                                        else
                                        {
                                            try
                                            {
                                                if (dbDate.Equals(System.DateTime.MinValue))
                                                {
                                                    strSql += "," + name + "='" + toSqlDate((Google.Api.Ads.Dfp.v201605.DateTime)value) + "'";
                                                    try
                                                    {
                                                        if (dataReader[name + "timeZoneID"] != null)
                                                            strSql += "," + name + "timeZoneID='" + ((Google.Api.Ads.Dfp.v201605.DateTime)value).timeZoneID.ToString().Replace("'", "''") + "'";
                                                    }
                                                    catch (Exception) { }
                                                }
                                                else if (dataReader.GetDateTime(i).CompareTo(value) != 0)
                                                {
                                                    strSql += "," + name + "='" + toSqlDate((Google.Api.Ads.Dfp.v201605.DateTime)value) + "'";
                                                    try
                                                    {
                                                        if (dataReader[name + "timeZoneID"] != null)
                                                            strSql += "," + name + "timeZoneID='" + ((Google.Api.Ads.Dfp.v201605.DateTime)value).timeZoneID.ToString().Replace("'", "''") + "'";
                                                    }
                                                    catch (Exception) { }
                                                }
                                            }
                                            catch (Exception)
                                            {
                                                strSql += "," + name + "='" + toSqlDate((Google.Api.Ads.Dfp.v201605.DateTime)value) + "'";
                                                try
                                                {
                                                    if (dataReader[name + "timeZoneID"] != null)
                                                        strSql += "," + name + "timeZoneID='" + ((Google.Api.Ads.Dfp.v201605.DateTime)value).timeZoneID.ToString().Replace("'", "''") + "'";
                                                }
                                                catch (Exception) { }
                                            }
                                        }
                                    }
                                    else if (dfpValueIsNull) // Not both are null, so if dfpValue is null, then the value has been deleted, no need to compare.
                                    {
                                        strSql += "," + name + "=null";
                                    }
                                    else if (!dfpValueIsNull || !dbValueIsNull)
                                    {

                                        switch (type)
                                        {
                                            case "VARCHAR":
                                            case "NVARCHAR":
                                            case "TINYTEXT":
                                            case "TEXT":
                                            case "MEDIUMTEXT":
                                            case "LONGTEXT":
                                                if (dataReader.GetValue(i).ToString().CompareTo(value.ToString()) != 0)
                                                    if (property.PropertyType.Name == "String")
                                                        strSql += "," + name + "='" + (value).Replace("'", "''").Replace(@"\", @"\\").Replace(@"\''", @"\\''") + "'";
                                                    else
                                                        strSql += "," + name + "='" + ((string)value.ToString().Replace("'", "''")) + "'";
                                                break;

                                            case "BIT":
                                            case "BINARY":
                                            case "BINARY(1)":
                                                if ((Convert.ToInt16(dataReader[name]) == 1) ^ value) // XOR
                                                    if (value)
                                                        strSql += "," + name + "=1";
                                                    else
                                                        strSql += "," + name + "=0";
                                                break;

                                            default:
                                                if (value.GetType().Name == "Money")
                                                {
                                                    if (!dataReader.GetValue(i).Equals(Convert.ToInt64(((Google.Api.Ads.Dfp.v201605.Money)value).microAmount)))
                                                        strSql += "," + name + "=" + Convert.ToInt64(((Google.Api.Ads.Dfp.v201605.Money)value).microAmount);

                                                    if (dataReader[name + "currencyCode"] != null)
                                                    {
                                                        if (dataReader[name + "currencyCode"].ToString() != ((Google.Api.Ads.Dfp.v201605.Money)value).currencyCode)
                                                            strSql += "," + name + "currencyCode='" + ((Google.Api.Ads.Dfp.v201605.Money)value).currencyCode + "'";
                                                    }
                                                    i = i + 1; // skip the currencyCode field
                                                }
                                                else if (!dataReader.GetValue(i).Equals(value))
                                                    strSql += "," + name + "=" + value;
                                                break;
                                        }
                                    }
                                    #endregion
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        dbBase.log("Error processing field " + name);
                        throw new Exception("Error processing field " + name, ex);
                    }
                }

                #region update
                if (strSql != keyField + "=" + valueField)
                {
                    // Avoid filling up logs table
                    int nbchanges = 0;
                    string changes = "";
                    string[] sParam = strSql.Split(',');
                    for (int i = 1; i < sParam.Length; i++) // skip [0], it's keyfield, always there;
                    {
                        string[] sOpt = sParam[i].Split('=');
                        strSql = strSql.Replace("&comma;", ",").Replace("&equals;", "="); // put special chars back in place for serialized

                        switch (sOpt[0])
                        {   // Don't want to log changes if only these fields have been modified
                            case "costPerUnit":
                            case "creativePlaceholders_Sizes":
                            case "isMissingCreatives":
                            case "lastModifiedByApp":
                            case "lastModifiedDateTime":
                            case "lastModifiedDateTimetimeZoneID":
                            case "deliveryIndicator_actualDeliveryPercentage":
                            case "deliveryIndicator_expectedDeliveryPercentage":
                            case "previewUrl":
                            case "serialized":
                            case "size_isAspectRatio":
                            case "snippet":
                            case "stats_clicksDelivered":
                            case "stats_impressionsDelivered":
                            case "stats_videoCompletionsDelivered":
                            case "stats_videoStartsDelivered":
                            case "status":
                            case "totalClicksDelivered":
                            case "totalImpressionsDelivered":
                                break;
                            default:
                                changes = changes + "," + sParam[i];
                                nbchanges++;
                                break;
                        }
                    }
                    if (nbchanges > 0)
                    {
                        if (adding)
                        {
                            dbBase.log(" ... Added");
                        }
                        else
                        {
                            dbBase.log(" ... Modified");
                            //dbBase.execSqlCommand("INSERT INTO 0_logs (ObjectName,ObjectValue,Info,Data) VALUES('" + keyField + "'," + valueField + ",'Update','" + changes.Substring(1).Replace("'", "''") + "')");
                        }
                    }
                    strSql = strSql.Replace("\\\\\\''", "\\\\''");
                    MySqlConnection updateConn = dbBase.getNewConnection();
                    dbBase.execSqlCommand("UPDATE " + tableName + " SET " + strSql + " WHERE " + keyField + "=" + valueField, updateConn);
                    updateConn.Close();
                    updateConn.Dispose();
                }
                #endregion
            }
            catch (Exception ex)
            {
                throw new Exception("Error comparing DFP object with source", ex);
            }
            #endregion

            #region customFields
            try
            {
                if (obj.GetType().GetProperty("customFieldValues") != null)
                {
                    if (obj.customFieldValues != null)
                    {
                        MySqlConnection conn2 = dbBase.getNewConnection();
                        dbBase.execSqlCommand("UPDATE dfp_customfields_links SET working='" + dbBase.guid + "' WHERE " + keyField + "=" + valueField, conn2);
                        foreach (BaseCustomFieldValue customFieldValue in obj.customFieldValues)
                        {
                            if (customFieldValue.GetType().ToString().Split('.').Last() == "DropDownCustomFieldValue")
                            {
                                DropDownCustomFieldValue dropDownCustomFieldValue = (DropDownCustomFieldValue)customFieldValue;
                                dbBase.execSqlCommand("INSERT INTO dfp_customfields_links (customFieldId, customFieldOptionId, " + keyField + ") values(" + dropDownCustomFieldValue.customFieldId + "," + dropDownCustomFieldValue.customFieldOptionId + ", " + valueField + ") ON DUPLICATE KEY UPDATE working=null", conn2);
                            }
                        }
                        dbBase.execSqlCommand("DELETE FROM dfp_customfields_links WHERE working='" + dbBase.guid + "' AND " + keyField + "=" + valueField, conn2);
                        conn2.Close();
                        conn2.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            #endregion
        }
        #endregion

        #region dates
        public static string toSqlDate(Google.Api.Ads.Dfp.v201605.DateTime dateTime)
        {
            string ret = "";
            if (dateTime != null)
            {
                ret += dateTime.date.year.ToString("0000");
                ret += "-";
                ret += dateTime.date.month.ToString("00");
                ret += "-";
                ret += dateTime.date.day.ToString("00");
                ret += " ";
                ret += dateTime.hour.ToString("00");
                ret += ":";
                ret += dateTime.minute.ToString("00");
                ret += ":";
                ret += dateTime.second.ToString("00");
            }
            return ret;
        }
        public static Google.Api.Ads.Dfp.v201605.Date toGoogleDate(System.DateTime dateTime)
        {
            Google.Api.Ads.Dfp.v201605.Date ret = new Google.Api.Ads.Dfp.v201605.Date();
            if (dateTime != null)
            {
                ret.year = dateTime.Year;
                ret.month = dateTime.Month;
                ret.day = dateTime.Day;
            }
            return ret;
        }
        public static bool DifferentDate(System.DateTime sqlDateTime, Google.Api.Ads.Dfp.v201605.DateTime value)
        {
            bool ret = false;
            if (sqlDateTime.Year != value.date.year) ret = true;
            if (sqlDateTime.Month != value.date.month) ret = true;
            if (sqlDateTime.Day != value.date.day) ret = true;
            if (sqlDateTime.Hour != value.hour) ret = true;
            if (sqlDateTime.Minute != value.minute) ret = true;
            if (sqlDateTime.Second != value.second) ret = true;
            return ret;
        }
        #endregion

        #region creatives
        public static void createDuplicateCreative(long oldCreativeId, List<long> orders)
        {
            dbBase.log(" *** policyViolation");

            DfpUser interfaceUser = com.hearst.utils.OAuth2.getDfpUser();

            CreativeService creativeService = (CreativeService)interfaceUser.GetService(DfpService.v201605.CreativeService);
            StatementBuilder creativeStatementBuilder = new StatementBuilder()
                .Where("id = " + oldCreativeId);
            CreativePage creativePage = new CreativePage();

            LineItemCreativeAssociationService licaService = (LineItemCreativeAssociationService)interfaceUser.GetService(DfpService.v201605.LineItemCreativeAssociationService);
            StatementBuilder licaStatementBuilder = new StatementBuilder()
                .Where("creativeId = " + oldCreativeId);
            LineItemCreativeAssociationPage licaPage = new LineItemCreativeAssociationPage();
            List<LineItemCreativeAssociation> licas = new List<LineItemCreativeAssociation>();

            string lineitems = "";
  
            string emailTo = "sgagnier@hearst.com";
            if (ConfigurationManager.AppSettings["creativePolicyViolations"] != null)
                emailTo = ConfigurationManager.AppSettings["creativePolicyViolations"];

            try
            {
                string token = " - dupsga";
                creativePage = creativeService.getCreativesByStatement(creativeStatementBuilder.ToStatement());
                Creative creative = creativePage.results[0];
                if (!creative.name.EndsWith(token))
                { 
                    // Current creative
                    string creativeName = creative.name;
                    long creativeId = creative.id;

                    licaPage = licaService.getLineItemCreativeAssociationsByStatement(licaStatementBuilder.ToStatement());
                    // Process each licas
                    foreach (LineItemCreativeAssociation lica in licaPage.results)
                    {
                        LineItemService lineitemService = (LineItemService)interfaceUser.GetService(DfpService.v201605.LineItemService);
                        StatementBuilder lineitemStatementBuilder = new StatementBuilder()
                            .Where("id = " + lica.lineItemId);
                        LineItemPage lineitemPage = lineitemService.getLineItemsByStatement(lineitemStatementBuilder.ToStatement());

                        foreach (LineItem lineitem in lineitemPage.results)
                        {
                            //Only process if lineitem is not archived
                            if (!lineitem.isArchived && orders.Contains(lineitem.orderId))
                            {  
                                lica.stats = null;
                                lineitems += ((lineitems == "") ? "" : ",") + lica.lineItemId;
                                dbBase.log("associating with lineitemId " + lica.lineItemId);
                                licas.Add(lica);
                            }
                        }
                    }
                    // Only write when there are changes
                    if (licas.Count > 0)
                    {
                        // New copy of the creative
                        creative.id = 0;
                        creative.name = creativeName;
                        Creative[] createdCreatives = creativeService.createCreatives(new Creative[] { creative });
                        dbBase.log("New creativeId " + createdCreatives[0].id);

                        // Set Licas creative id to the new one.
                        foreach(LineItemCreativeAssociation lica in licas)
                            lica.creativeId = createdCreatives[0].id;

                        // Write Licas
                        licaService.createLineItemCreativeAssociations(licas.ToArray());

                        // Update original creative
                        creative.id = creativeId;
                        creative.name = creativeName + token;
                        Creative[] oldCreatives = creativeService.createCreatives(new Creative[] { creative });

                        // Email notification
                        dbBase.sendMail(emailTo, "Creative disabled for policy Violation", "A new creative has been created.\nId: " + createdCreatives[0].id + "\nAnd associated with lineitems: " + lineitems);
                    }
                }
            }
            catch (Exception ex)
            {
                dbBase.sendMail(emailTo, "Creative disabled for policy Violation", "Unable to duplicate creative " + oldCreativeId + "\n" + ex.Message);
            }
        }
        public static void getOrdersFromAdvertisers(List<long> advertisers, ref List<long>orders)
        {
            try
            {
                DfpUser interfaceUser = com.hearst.utils.OAuth2.getDfpUser();
                OrderService orderService = (OrderService)interfaceUser.GetService(DfpService.v201605.OrderService);
                StatementBuilder orderStatementBuilder = new StatementBuilder()
                    .Where("advertiserId in (" + String.Join<long>(",", advertisers) + ")");
                OrderPage orderPage = orderService.getOrdersByStatement(orderStatementBuilder.ToStatement());

                foreach (Order order in orderPage.results)
                    orders.Add(order.id);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        #endregion

        #region serialize
        public static string getSerialized(dynamic obj)
        {
            string ret = "";
            try
            {
                XmlDocument myXml = new XmlDocument();
                XPathNavigator xNav = myXml.CreateNavigator();
                XmlSerializer x = new XmlSerializer(obj.GetType());
                using (var xs = xNav.AppendChild())
                {
                    x.Serialize(xs, obj);
                }
                ret = myXml.OuterXml.Replace("'", "&apos;").Replace(",", "&comma;").Replace("=", "&equals;");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return ret;
        }
        #endregion
    }
}
