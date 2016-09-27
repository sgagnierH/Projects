using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Serialization;
using Tc.TcMedia.Scheduler;
using MySql.Data;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;
using Google.Api.Ads.Common.Util;
using MySql.Data.MySqlClient;
using System.Reflection;

namespace Tc.TcMedia.Dfp
{
    public class DFPBase
    {
        #region check
        public static void checkDfpObject(Process process, string tableName, string keyfield, dynamic obj)
        {
            bool added = false;
            MySqlConnection conn = Db.getConnection();
            MySqlDataReader dataReader = null;
            int retries = 0;
            bool success = false;
            while (retries < 1 && !success)
            {
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
                    processCheck(process, obj, dataReader, tableName, keyfield, obj.id, added);
                    success = true;
                }
                catch (Exception ex)
                {
                    if (retries++ == 5)
                    {
                        process.log("Error comparing DFP objects");
                        throw new Exception("Error comparing DFP objects with " + keyfield + "=" + obj.id, ex);
                    }
                }
                finally
                {
                    if (dataReader != null)
                        if (!dataReader.IsClosed)
                            dataReader.Close();
                    conn.Close();
                }
            }
        }
        public static void processCheck(Process process, dynamic obj, MySqlDataReader dataReader, string tableName, string keyField, long valueField, bool adding = false)
        {
            processCheck(process, obj, dataReader, tableName, keyField, valueField.ToString(), adding);
        }
        public static void processCheck(Process process, dynamic obj, MySqlDataReader dataReader, string tableName, string keyField, string valueField, bool adding = false)
        /***
         * Scrolls through the database table and compare fields with existing in the object for a change
         **/
        {
            var ass = typeof(DFPBase).Assembly;
            var cul = new CultureInfo("fr-CA");
            cul.NumberFormat.NumberDecimalSeparator = ".";
            CultureInfo.DefaultThreadCurrentCulture = cul;

            string name = null;
            #region tableFields
            try
            {
                Db db = new Db(process);
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
                            string type = dataReader.GetDataTypeName(i).ToString();
                            property = obj.GetType().GetProperty(name);

                            #region subObject
                            if (obj.GetType().GetProperty(name) == null && name.IndexOf('_') > 1)
                            {
                                string[] tmpName = name.Split('_');
                                if (obj.GetType().GetProperty(tmpName[0]) != null)
                                {
                                    property = obj.GetType().GetProperty(tmpName[0]);
                                }
                                else
                                {
                                    if (name.IndexOf("hasTargeting") == 0)
                                    {
                                        if (((LineItem)obj).targeting != null)
                                        {
                                            Object pobj = null;
                                            switch (name.Substring(13))
                                            {
                                                case "contentTargeting": pobj = ((LineItem)obj).targeting.contentTargeting; break;
                                                case "customTargeting": pobj = ((LineItem)obj).targeting.customTargeting; break;
                                                case "dayPartTargeting": pobj = ((LineItem)obj).targeting.dayPartTargeting; break;
                                                case "geoTargeting": pobj = ((LineItem)obj).targeting.geoTargeting; break;
                                                case "inventoryTargeting": pobj = ((LineItem)obj).targeting.inventoryTargeting; break;
                                                case "technologyTargeting": pobj = ((LineItem)obj).targeting.technologyTargeting; break;
                                                case "userDomainTargeting": pobj = ((LineItem)obj).targeting.userDomainTargeting; break;
                                                case "videoPositionTargeting": pobj = ((LineItem)obj).targeting.videoPositionTargeting; break;
                                            }

                                            if (pobj != null)
                                            {
                                                if (Convert.ToInt16(dataReader[name]) == 0)
                                                    strSql = strSql + "," + name + "=1";
                                            }
                                            else
                                            {
                                                if (Convert.ToInt16(dataReader[name]) == 1)
                                                    strSql = strSql + "," + name + "=0";
                                            }
                                        }
                                        else
                                        {
                                            if (Convert.ToInt16(dataReader[name]) == 1)
                                                strSql = strSql + "," + name + "=0";
                                        }
                                    }
                                }
                            }
                            #endregion

                            if (property == null)
                            {
                                #region Serialized
                                if ((tableName == "dfp_lineitems" || tableName == "dfp_orders") && name == "serialized")
                                {
                                    if (obj.lastModifiedByApp == "webMetho") // modifié par l'app Web Method
                                    {
                                        List<BaseCustomFieldValue> customFieldValues = new List<BaseCustomFieldValue>();
                                        switch (tableName)
                                        {
                                            case "dfp_lineitems":
                                                LineItem lineitem = (LineItem)obj;
                                                bool updateFTPLineItem = false;
                                                if ((lineitem.status.ToString() == "INACTIVE" || lineitem.status.ToString() == "READY" || lineitem.status.ToString() == "DELIVERING" || lineitem.status.ToString() == "PAUSED") &&
                                                   (lineitem.frequencyCaps == null || lineitem.budget.microAmount == 0 || lineitem.targeting.geoTargeting == null || lineitem.targeting.customTargeting == null || lineitem.targeting.technologyTargeting == null))
                                                {
                                                    // Web Method a écrasé?
                                                    if (!dataReader.IsDBNull(i))
                                                    {
                                                        // Il y avait des données qui ont été écrasées, on les remet
                                                        string body = "LineItem : " + obj.id + "\n";
                                                        body = body + "------\nLineItem Received\n" + DFPBase.getSerialized(obj).Replace("&equals;", "=") + "\n";

                                                        string fromDb = dataReader.GetString("serialized");
                                                        if (fromDb != "")
                                                        {
                                                            LineItem deserialized = DFPBase.getDeserializedLineItem(dataReader.GetString("serialized"));
                                                            if (deserialized.targeting != null)
                                                                obj.targeting = deserialized.targeting;
                                                            obj.budget = deserialized.budget;
                                                            obj.appliedLabels = deserialized.appliedLabels;
                                                            obj.frequencyCaps = deserialized.frequencyCaps;
                                                            if (deserialized.costPerUnit != null)
                                                                obj.costPerUnit = deserialized.costPerUnit;
                                                            if (deserialized.customFieldValues != null)
                                                                obj.customFieldValues = deserialized.customFieldValues;
                                                            updateFTPLineItem = true;

                                                            body = body + "------\nLineItem Updated\n" + DFPBase.getSerialized(obj).Replace("&equals;", "=");
                                                            string emailTo = "tcapiaccess@tc.tc,campagnes@tc.tc";
                                                            if (ConfigurationManager.AppSettings["salesforceOverwrite"] != null)
                                                                emailTo = ConfigurationManager.AppSettings["salesforceOverwrite"] + ",tcapiaccess@tc.tc";
                                                            Db.sendMail(process, emailTo, "Local lineitem overwritten", body);
                                                        }
                                                    }
                                                }
                                                if (obj.customFieldValues == null)
                                                {
                                                    LineItem lineItemLoc = (LineItem)obj;
                                                    DropDownCustomFieldValue customField = new DropDownCustomFieldValue();
                                                    customField.customFieldId = 9525;
                                                    if (lineItemLoc.name.IndexOf("RON") > -1)
                                                        customField.customFieldOptionId = 26805;
                                                    else if (lineItemLoc.name.IndexOf("ROC") > -1)
                                                        customField.customFieldOptionId = 26685;
                                                    else
                                                        customField.customFieldOptionId = 26565;
                                                    customFieldValues.Add(customField);
                                                    lineItemLoc.customFieldValues = customFieldValues.ToArray();
                                                    updateFTPLineItem = true;
                                                }   
                                                if(updateFTPLineItem)
                                                {
                                                    List<LineItem> lineItemList = new List<LineItem>();
                                                    lineItemList.Add(obj);
                                                    DfpUser user = Tc.TcMedia.Dfp.Auth.getDfpUser();
                                                    LineItemService lineItemService = (LineItemService)user.GetService(DfpService.v201508.LineItemService);
                                                    lineItemService.updateLineItems(lineItemList.ToArray());
                                                }
                                                break;
                                            case "dfp_orders":
                                                Order order = (Order)obj;
                                                bool updateDFPOrder = false;
                                                if (order.status.ToString() == "APPROVED" && order.customFieldValues == null)
                                                {
                                                    // Web Method a écrasé?
                                                    if (!dataReader.IsDBNull(i))
                                                    {
                                                        // Il y avait des données qui ont été écrasées, on les remet
                                                        string body = "Order : " + obj.id + "\n";
                                                        body = body + "------\nOrder Received\n" + DFPBase.getSerialized(obj).Replace("&equals;", "=") + "\n";

                                                        string fromDb = dataReader.GetString("serialized");
                                                        if (fromDb != "")
                                                        {
                                                            Order deserialized = DFPBase.getDeserializedOrder(dataReader.GetString("serialized"));
                                                            if (deserialized.customFieldValues != null)
                                                            {
                                                                obj.customFieldValues = deserialized.customFieldValues;
                                                                updateDFPOrder = true;
                                                            }

                                                            body = body + "------\nOrder Updated\n" + DFPBase.getSerialized(obj).Replace("&equals;", "=");
                                                            string emailTo = "tcapiaccess@tc.tc,campagnes@tc.tc";
                                                            if (ConfigurationManager.AppSettings["salesforceOverwrite"] != null)
                                                                emailTo = ConfigurationManager.AppSettings["salesforceOverwrite"] + ",tcapiaccess@tc.tc";
                                                            Db.sendMail(process, emailTo, "Local order overwritten", body);
                                                        }
                                                    }
                                                }
                                                if (obj.customFieldValues == null)
                                                {
                                                    Order orderLoc = (Order)obj;
                                                    DropDownCustomFieldValue customField = new DropDownCustomFieldValue();
                                                    customField.customFieldId = 10245;
                                                    customField.customFieldOptionId = 31365;
                                                    customFieldValues.Add(customField);
                                                    orderLoc.customFieldValues = customFieldValues.ToArray();
                                                    updateDFPOrder = true;
                                                }   

                                                if(updateDFPOrder)
                                                {
                                                    List<Order> orderList = new List<Order>();
                                                    orderList.Add(obj);
                                                    DfpUser user = Tc.TcMedia.Dfp.Auth.getDfpUser();
                                                    OrderService orderService = (OrderService)user.GetService(DfpService.v201508.OrderService);
                                                    orderService.updateOrders(orderList.ToArray());
                                                }
                                                break;
                                        }
                                    }

                                    // Enregistrer les infos
                                    strSql += ",serialized='" + DFPBase.getSerialized(obj) + "'";
                                }
                                #endregion
                                #region Proposal agencies
                                if (tableName == "dfp_proposals" && name == "agencyIds")
                                {
                                    if (obj.agencies == null)
                                    {
                                        if (!dataReader.IsDBNull(i))
                                            strSql += ",agencyIds=null";
                                    }
                                    else
                                    {
                                        string ids = null;
                                        foreach (ProposalCompanyAssociation pca in obj.agencies)
                                            ids = ids + ((ids == null) ? "" : ",") + pca.companyId;

                                        if (dataReader.IsDBNull(i))
                                            strSql += ",agencyIds='" + ids + "'";
                                        else if (dataReader.GetString("agencyIds") != ids)
                                            strSql += ",agencyIds='" + ids + "'";
                                    }
                                }
                                #endregion
                                #region snippet // creatives
                                else if (tableName == "dfp_creatives" && name == "appnexusId")
                                {
                                    value = null;
                                    string creativeType = (((Creative)obj).GetType().ToString()).Split('.').Last<string>();
                                    string snippet = null;
                                    if (creativeType == "CustomCreative")
                                    {
                                        snippet = ((CustomCreative)obj).htmlSnippet;
                                    }
                                    else if (creativeType == "ThirdPartyCreative")
                                    {
                                        snippet = ((ThirdPartyCreative)obj).snippet;
                                        if (snippet == null)
                                            snippet = ((ThirdPartyCreative)obj).expandedSnippet;
                                    }

                                    if (snippet != null)
                                    {
                                        value = extractAppnexusId(snippet);
                                    }

                                    if (value == null)
                                    {
                                        if (dataReader["appnexusId"].ToString() != "")
                                            strSql += ",appnexusId=null";
                                        else if ("&" + dataReader["appnexusId"].ToString() != "&" + value)
                                            strSql += ",appnexusId=" + value;
                                    }
                                    else if (!value.Equals(dataReader["appnexusId"].ToString()))
                                        strSql += ",appnexusId=" + value;

                                    if (dataReader["creativeType"].ToString() != creativeType)
                                        strSql += ",creativeType='" + creativeType + "'";
                                }
                                #endregion
                            }
                            else
                            {
                                value = property.GetValue(obj,null);
                                string dfpType = (value == null) ? null : value.GetType().Name;

                                #region creativeWrapper // htmlSnippet
                                if (property.PropertyType.Name == "CreativeWrapperHtmlSnippet")
                                {
                                    CreativeWrapperHtmlSnippet snippet = (CreativeWrapperHtmlSnippet)value;
                                    if (dataReader[name].ToString() != snippet.htmlSnippet.Replace("'", "''"))
                                    {
                                        strSql += "," + name + "='" + snippet.htmlSnippet.Replace("'","''") + "'";
                                    }
                                }
                                #endregion
                                #region policyViolations // creatives
                                else if (property.PropertyType.Name == "CreativePolicyViolation[]")
                                {
                                    if (obj.policyViolations != null)
                                    {
                                        Creative creative = (Creative)obj;
                                        string policyViolations = "";
                                        foreach (CreativePolicyViolation violation in creative.policyViolations)
                                        {
                                            policyViolations += ((policyViolations == "") ? "" : ",") + violation.ToString();
                                        }
                                        if (dataReader["policyViolations"].ToString() != policyViolations)
                                        {
                                            strSql += ",policyViolations='" + policyViolations + "'";

                                            if (creative.GetType().Name == "CustomCreative" || creative.GetType().Name == "ThirdPartyCreative")
                                            {
                                                process.log("Duplicating policyViolation detected creative");
                                                DFPBase.createDuplicateCreative(process, obj.id);
                                            }
                                        }
                                    }
                                    else if ((dataReader.GetType().GetProperty(name) != null))
                                    {
                                        strSql += ",policyViolations=null";
                                    }
                                }
                                #endregion
                                #region grpSettings // lineItem
                                else if (property.PropertyType.Name == "GrpSettings")
                                {
                                    if (value == null)
                                    {
                                        if ((string)dataReader["grpSettings_minTargetAge"].ToString() != "")
                                            strSql += ",grpSettings_minTargetAge=null";

                                        if ((string)dataReader["grpSettings_maxTargetAge"].ToString() != "")
                                            strSql += ",grpSettings_madTargetAge=null";

                                        if ((string)dataReader["grpSettings_targetGender"].ToString() != "")
                                            strSql += ",grpSettings_targetGender=null";

                                        if ((string)dataReader["grpSettings_provider"].ToString() != "")
                                            strSql += ",grpSettings_provider=null";
                                    }
                                    else
                                    {

                                        if (Convert.ToInt64(dataReader["grpSettings_minTargetAge"]) != ((Google.Api.Ads.Dfp.v201508.GrpSettings)value).minTargetAge)
                                            strSql += ",grpSettings_minTargetAge='" + ((Google.Api.Ads.Dfp.v201508.GrpSettings)value).minTargetAge.ToString().Replace("'", "''") + "'";

                                        if (Convert.ToInt64(dataReader["grpSettings_maxTargetAge"]) != ((Google.Api.Ads.Dfp.v201508.GrpSettings)value).maxTargetAge)
                                            strSql += ",grpSettings_maxTargetAge='" + ((Google.Api.Ads.Dfp.v201508.GrpSettings)value).maxTargetAge.ToString().Replace("'", "''") + "'";

                                        if ("&" + dataReader["grpSettings_targetGender"].ToString() != "&" + ((Google.Api.Ads.Dfp.v201508.GrpSettings)value).targetGender.ToString())
                                            strSql += ",grpSettings_targetGender='" + ((Google.Api.Ads.Dfp.v201508.GrpSettings)value).targetGender.ToString().Replace("'", "''") + "'";

                                        if ("&" + dataReader["grpSettings_provider"].ToString() != "&" + ((Google.Api.Ads.Dfp.v201508.GrpSettings)value).provider.ToString())
                                            strSql += ",grpSettings_provider='" + ((Google.Api.Ads.Dfp.v201508.GrpSettings)value).provider.ToString().Replace("'", "''") + "'";
                                    }
                                    i = i + 3; // skip all the 4 lines
                                }
                                #endregion
                                #region size
                                else if (property.PropertyType.Name == "Size")
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
                                        if (Convert.ToBoolean(dataReader["size_isAspectRatio"]) != ((Google.Api.Ads.Dfp.v201508.Size)value).isAspectRatio)
                                            strSql += ",size_isAspectRatio=" + ((Google.Api.Ads.Dfp.v201508.Size)value).isAspectRatio;

                                        if (Convert.ToInt32(dataReader["size_height"]) != ((Google.Api.Ads.Dfp.v201508.Size)value).height)
                                            strSql += ",size_height=" + ((Google.Api.Ads.Dfp.v201508.Size)value).height;

                                        if (Convert.ToInt32(dataReader["size_width"]) != ((Google.Api.Ads.Dfp.v201508.Size)value).width)
                                            strSql += ",size_width=" + ((Google.Api.Ads.Dfp.v201508.Size)value).width;
                                    }
                                    i = i + 2;
                                }
                                #endregion
                                #region stats // lineItem
                                else if (property.PropertyType.Name == "Stats")
                                {
                                    if (value == null)
                                    {
                                        if ((string)dataReader["stats_clicksDelivered"].ToString() != "0")
                                            strSql += ",stats_clicksDelivered=0";

                                        if ((string)dataReader["stats_impressionsDelivered"].ToString() != "0")
                                            strSql += ",stats_impressionsDelivered=0";

                                        if ((string)dataReader["stats_videoCompletionsDelivered"].ToString() != "0")
                                            strSql += ",stats_videoCompletionsDelivered=0";

                                        if ((string)dataReader["stats_videoStartsDelivered"].ToString() != "0")
                                            strSql += ",stats_videoStartsDelivered=0";
                                    }
                                    else
                                    {
                                        if (Convert.ToInt64(dataReader["stats_clicksDelivered"]) != ((Google.Api.Ads.Dfp.v201508.Stats)value).clicksDelivered)
                                            strSql += ",stats_clicksDelivered='" + ((Google.Api.Ads.Dfp.v201508.Stats)value).clicksDelivered.ToString().Replace("'", "''") + "'";

                                        if (Convert.ToInt64(dataReader["stats_impressionsDelivered"]) != ((Google.Api.Ads.Dfp.v201508.Stats)value).impressionsDelivered)
                                            strSql += ",stats_impressionsDelivered='" + ((Google.Api.Ads.Dfp.v201508.Stats)value).impressionsDelivered.ToString().Replace("'", "''") + "'";

                                        if (Convert.ToInt64(dataReader["stats_videoCompletionsDelivered"]) != ((Google.Api.Ads.Dfp.v201508.Stats)value).videoCompletionsDelivered)
                                            strSql += ",stats_videoCompletionsDelivered='" + ((Google.Api.Ads.Dfp.v201508.Stats)value).videoCompletionsDelivered.ToString().Replace("'", "''") + "'";

                                        if (Convert.ToInt64(dataReader["stats_videoStartsDelivered"]) != ((Google.Api.Ads.Dfp.v201508.Stats)value).videoStartsDelivered)
                                            strSql += ",stats_videoStartsDelivered='" + ((Google.Api.Ads.Dfp.v201508.Stats)value).videoStartsDelivered.ToString().Replace("'", "''") + "'";
                                    }
                                    i = i + 3; // skip all 4 lines
                                }
                                #endregion
                                #region deliveryIndicator // lineItem
                                else if (property.PropertyType.Name == "DeliveryIndicator")
                                {
                                    if (value == null)
                                    {
                                        if ((string)dataReader["deliveryIndicator_actualDeliveryPercentage"].ToString() != "0")
                                            strSql += ",deliveryIndicator_actualDeliveryPercentage=0";

                                        if ((string)dataReader["deliveryIndicator_expectedDeliveryPercentage"].ToString() != "0")
                                            strSql += ",deliveryIndicator_expectedDeliveryPercentage=0";
                                    }
                                    else
                                    {
                                        if (!dataReader["deliveryIndicator_actualDeliveryPercentage"].Equals(((DeliveryIndicator) value).actualDeliveryPercentage))
                                            strSql += ",deliveryIndicator_actualDeliveryPercentage=" + ((DeliveryIndicator) value).actualDeliveryPercentage;

                                        if (!dataReader["deliveryIndicator_expectedDeliveryPercentage"].Equals(((DeliveryIndicator)value).expectedDeliveryPercentage))
                                            strSql += ",deliveryIndicator_expectedDeliveryPercentage=" + ((DeliveryIndicator)value).expectedDeliveryPercentage;
                                    }
                                    i = i + 1; // skip both lines
                                }
                                #endregion
                                #region primaryGoal // lineItem
                                else if (property.PropertyType.Name == "Goal")
                                {
                                    if (value == null)
                                    {
                                        if ((string)dataReader["primaryGoal_goalType"].ToString() != "")
                                            strSql += ",primaryGoal_goalType=null";

                                        if ((string)dataReader["primaryGoal_units"].ToString() != "")
                                            strSql += ",primaryGoal_units=null";

                                        if ((string)dataReader["primaryGoal_unitType"].ToString() != "")
                                            strSql += ",primaryGoal_unitType=null";
                                    }
                                    else
                                    {
                                        if ("&" + dataReader["primaryGoal_goalType"].ToString() != "&" + ((Google.Api.Ads.Dfp.v201508.Goal)value).goalType.ToString())
                                            strSql += ",primaryGoal_goalType='" + ((Google.Api.Ads.Dfp.v201508.Goal)value).goalType.ToString().Replace("'", "''") + "'";

                                        if (Convert.ToInt64(dataReader["primaryGoal_units"]) != ((Google.Api.Ads.Dfp.v201508.Goal)value).units)
                                            strSql += ",primaryGoal_units=" + ((Google.Api.Ads.Dfp.v201508.Goal)value).units;

                                        if ("&" + dataReader["primaryGoal_unitType"].ToString() != "&" + ((Google.Api.Ads.Dfp.v201508.Goal)value).unitType.ToString())
                                            strSql += ",primaryGoal_unitType='" + ((Google.Api.Ads.Dfp.v201508.Goal)value).unitType.ToString().Replace("'", "''") + "'";

                                    }
                                    i = i + 2; // skip all 3 lines about primaryGoal
                                }
                                #endregion
                                #region creativePlaceholders // lineitem
                                else if (property.PropertyType.Name == "CreativePlaceholder[]")
                                {
                                    if (value == null)
                                    {
                                        if (!dataReader.IsDBNull(i))
                                            strSql += ",creativePlaceholders_Sizes=null";
                                    }
                                    else
                                    {
                                        string sizes = "";
                                        foreach (CreativePlaceholder creative in value)
                                            sizes += ((sizes == "") ? "" : ",") + creative.size.width + "x" + creative.size.height;

                                        if (dataReader.IsDBNull(i))
                                        {
                                            strSql += ",creativePlaceholders_Sizes='" + sizes + "'";
                                        }
                                        else if(!dataReader.GetString(i).Equals(sizes))
                                        {
                                            strSql += ",creativePlaceholders_Sizes='" + sizes + "'";
                                        }
                                    }
                                }
                                #endregion
                                #region Proposal Advertiser
                                else if (property.PropertyType.Name == "ProposalCompanyAssociation")
                                {
                                    if(value == null)
                                    {
                                        if (!dataReader.IsDBNull(i))
                                            strSql += ",advertiser_companyId=null";
                                    }
                                    else if (dataReader.IsDBNull(i))
                                    {
                                        strSql += ",advertiser_companyId=" + ((Google.Api.Ads.Dfp.v201508.ProposalCompanyAssociation)value).companyId;
                                    }
                                    else if (Convert.ToInt64(dataReader["advertiser_companyId"]) != ((Google.Api.Ads.Dfp.v201508.ProposalCompanyAssociation)value).companyId)
                                    {
                                        strSql += ",advertiser_companyId=" + ((Google.Api.Ads.Dfp.v201508.ProposalCompanyAssociation)value).companyId;
                                    }
                                }
                                #endregion
                                #region Proposal primarySalesperson_userID
                                else if (property.PropertyType.Name == "SalespersonSplit")
                                {
                                    if (value == null)
                                    {
                                        if (!dataReader.IsDBNull(i))
                                            strSql += ",primarySalesperson_userId=null";
                                    }
                                    else if (dataReader.IsDBNull(i))
                                    {
                                        strSql += ",primarySalesperson_userId=" + ((Google.Api.Ads.Dfp.v201508.SalespersonSplit)value).userId;
                                    }
                                    else if (Convert.ToInt64(dataReader["primarySalesperson_userId"]) != ((Google.Api.Ads.Dfp.v201508.SalespersonSplit)value).userId)
                                    {
                                        strSql += ",primarySalesperson_userId=" + ((Google.Api.Ads.Dfp.v201508.SalespersonSplit)value).userId;
                                    }
                                }
                                #endregion
                                else
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
                                        int k = 1;
                                    }
                                    else if(property.PropertyType.Name == "Int64[]")
                                    {
                                        string ids = "";
                                        foreach (long id in value)
                                            ids = ids + ((ids == "") ? "" : ",") + id;

                                        if (dataReader[name] != ids)
                                            strSql += "," + name + "='" + ids + "'";
                                    }
                                    else if (type == "DATETIME")
                                    {
                                        System.DateTime dbDate = System.DateTime.MinValue;
                                        try
                                        {
                                            dbDate = dataReader.GetDateTime(name);
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
                                                    strSql += "," + name + "='" + toMySqlDate((Google.Api.Ads.Dfp.v201508.DateTime)value) + "'";
                                                    try
                                                    {
                                                        if (dataReader[name + "timeZoneID"] != null)
                                                            strSql += "," + name + "timeZoneID='" + ((Google.Api.Ads.Dfp.v201508.DateTime)value).timeZoneID.ToString().Replace("'", "''") + "'";
                                                    }
                                                    catch (Exception) { }
                                                }
                                                else if (Db.DifferentDate(dataReader.GetDateTime(i), value))
                                                {
                                                    strSql += "," + name + "='" + toMySqlDate((Google.Api.Ads.Dfp.v201508.DateTime)value) + "'";
                                                    try
                                                    {
                                                        if (dataReader[name + "timeZoneID"] != null)
                                                            strSql += "," + name + "timeZoneID='" + ((Google.Api.Ads.Dfp.v201508.DateTime)value).timeZoneID.ToString().Replace("'", "''") + "'";
                                                    }
                                                    catch (Exception) { }
                                                }
                                            }
                                            catch(Exception)
                                            {
                                                strSql += "," + name + "='" + toMySqlDate((Google.Api.Ads.Dfp.v201508.DateTime)value) + "'";
                                                try
                                                {
                                                    if (dataReader[name + "timeZoneID"] != null)
                                                        strSql += "," + name + "timeZoneID='" + ((Google.Api.Ads.Dfp.v201508.DateTime)value).timeZoneID.ToString().Replace("'", "''") + "'";
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
                                            case "TINYTEXT":
                                            case "TEXT":
                                            case "MEDIUMTEXT":
                                            case "LONGTEXT":
                                                if (dataReader.GetValue(i).ToString().CompareTo(value.ToString()) != 0)
                                                    if (property.PropertyType.Name == "String")
                                                        strSql += "," + name + "='" + (value).Replace("'", "''").Replace(@"\",@"\\").Replace(@"\''",@"\\''") + "'";
                                                    else
                                                        strSql += "," + name + "='" + ((string)value.ToString().Replace("'", "''")) + "'";
                                                break;

                                            case "BIT":
                                                if ((Convert.ToInt16(dataReader[name]) == 1) ^ value) // XOR
                                                    if (value)
                                                        strSql += "," + name + "=1";
                                                    else
                                                        strSql += "," + name + "=0";
                                                break;

                                            default:
                                                if (value.GetType().Name == "Money")
                                                {
                                                    if (!dataReader.GetValue(i).Equals(Convert.ToInt64(((Google.Api.Ads.Dfp.v201508.Money)value).microAmount)))
                                                        strSql += "," + name + "=" + Convert.ToInt64(((Google.Api.Ads.Dfp.v201508.Money)value).microAmount);

                                                    if (dataReader[name + "currencyCode"] != null)
                                                    {
                                                        if (dataReader[name + "currencyCode"].ToString() != ((Google.Api.Ads.Dfp.v201508.Money)value).currencyCode)
                                                            strSql += "," + name + "currencyCode='" + ((Google.Api.Ads.Dfp.v201508.Money)value).currencyCode + "'";
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
                    catch(Exception ex)
                    {
                        process.log("Error processing field " + name);
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
                        strSql = strSql.Replace("&comma;",",").Replace("&equals;","="); // put special chars back in place for serialized

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
                            process.log(" ... Added");
                        }
                        else
                        {
                            process.log(" ... Modified");
                            Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValue,Info,Data) VALUES('" + keyField + "'," + valueField + ",'Update','" + changes.Substring(1).Replace("'", "''") + "')");
                        }
                    }
                    Db.execSqlCommand(process, "UPDATE " + tableName + " SET " + strSql + " WHERE " + keyField + "=" + valueField);
                }
                #endregion
            }
            catch (Exception ex)
            {
                process.log(ex.Message);
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
                        Db.execSqlCommand(process, "UPDATE dfp_customfields_links SET working='" + process.guid + "' WHERE " + keyField + "=" + valueField);
                        foreach (BaseCustomFieldValue customFieldValue in obj.customFieldValues)
                        {
                            if (customFieldValue.GetType().ToString().Split('.').Last() == "DropDownCustomFieldValue")
                            {
                                DropDownCustomFieldValue dropDownCustomFieldValue = (DropDownCustomFieldValue)customFieldValue;
                                Db.execSqlCommand(process, "INSERT INTO dfp_customfields_links (customFieldId, customFieldOptionId, " + keyField + ") values(" + dropDownCustomFieldValue.customFieldId + "," + dropDownCustomFieldValue.customFieldOptionId + ", " + valueField + ") ON DUPLICATE KEY UPDATE working=null");
                            }
                        }
                        Db.execSqlCommand(process, "DELETE FROM dfp_customfields_links WHERE working='" + process.guid + "' AND " + keyField + "=" + valueField);
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            #endregion
        }
        #endregion

        #region lineItemType
        public static Dictionary<string, long> lineItemTypes = new Dictionary<string, long>();
        public static Dictionary<long, long> lineItemAdvertiserTypes = new Dictionary<long, long>();
        public static string checkLineItemType(Process process, LineItem lineItem, LineItemService lineItemService)
        {
            if (lineItem.isArchived) return "Archived";

            string ret = "";
            if(lineItemTypes.Count == 0)
            {
                lineItemTypes.Add("customFieldId", 9525);  // LineItem Type = 9525
                lineItemTypes.Add("Not found", 0);
                lineItemTypes.Add("Display ROC", 26685);
                lineItemTypes.Add("Display RON", 26805);
                lineItemTypes.Add("Display ROS", 26565);
                lineItemTypes.Add("Display RTB", 29805);
                lineItemTypes.Add("Header Bidding", 29925);
                lineItemTypes.Add("Newsletters", 26925);
                lineItemTypes.Add("NSBD", 27045);
                lineItemTypes.Add("Remnants", 29085);
                lineItemTypes.Add("Resold", 28965);
                lineItemTypes.Add("Test", 30045);
                lineItemTypes.Add("To specify", 27165);

                lineItemAdvertiserTypes.Add(31461765, 28965); //'Google - Remnant' : 'Resold'
                lineItemAdvertiserTypes.Add(32097765, 29085); //'AppNexus - Remnant' : 'Remnants'
                lineItemAdvertiserTypes.Add(75268965, 29085); //'Rubicon - Remnants' : 'Remnants'
                lineItemAdvertiserTypes.Add(54452685, 29085); //'LiveRail - Remnants' : 'Remnants'
            }

            List<BaseCustomFieldValue> customFieldValues = new List<BaseCustomFieldValue>();
            Dictionary<long, LineItem> lineItems = new Dictionary<long, LineItem>();

            bool found = false;
            bool update = false;
            long category = findLineItemCategory(process, lineItem);

            try
            {
                for (int fieldno = 0; fieldno < lineItem.customFieldValues.Count(); fieldno++)
                {
                    if (lineItem.customFieldValues[fieldno].customFieldId == lineItemTypes["customFieldId"])
                    {
                        DropDownCustomFieldValue customField = (DropDownCustomFieldValue)lineItem.customFieldValues[fieldno];
                        found = true;
                        if(category == customField.customFieldOptionId)
                        {
                            ret = "Ok";
                        }
                        else if(category != 0)
                        {
                            update = true;
                            customField.customFieldOptionId = category;
                            ret = "Update";
                        }
                        else
                        {
                            ret = "Forced";
                        }
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // do nothing
            }

            if (category == 0)
            {
                ret = "Not found";
            }
            else if (!found)
            {
                // Adding the new one
                DropDownCustomFieldValue customFieldValue = new DropDownCustomFieldValue();
                customFieldValue.customFieldId = lineItemTypes["customFieldId"];
                customFieldValue.customFieldOptionId = category;
                customFieldValues.Add(customFieldValue);

                lineItem.customFieldValues = customFieldValues.ToArray();
                update = true;
            }

            if(update)
            {
                // Update DFP
                lineItems.Clear();
                lineItems.Add(lineItem.id, lineItem);
                try
                {
                    lineItemService.updateLineItems(lineItems.Values.ToArray());
                    ret = "Added";
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            return ret;
        }
        private static long findLineItemCategory(Process process, LineItem lineItem)
        {
            string order = lineItem.orderName;
            string uorder = order.ToUpper();
            int len_lineitem = lineItem.name.Length;
            string ulineitem = lineItem.name.ToUpper();
            string uall = uorder + " " + ulineitem;
            string rvalue = "Not found";

            MySqlDataReader dataReader = Db.getMySqlReader(process, "SELECT advertiserId FROM dfp_orders WHERE orderId=" + lineItem.orderId);
            if (dataReader.Read())
            {
                long advertiserId = dataReader.GetInt64("advertiserId");
                dataReader.Close();
                if (lineItemAdvertiserTypes.ContainsKey(advertiserId))
                {
                    return (lineItemAdvertiserTypes[advertiserId]);
                }
            }
            else
                dataReader.Close();

            if (order.Length == 10 && order.StartsWith("O"))
            {
                if (ulineitem.IndexOf("COMBO 2") >= 10)
                {
                    if (ulineitem.IndexOf("RON") >= 10)
                        rvalue = "Display RON";
                    else
                        rvalue = "Display ROC";
                }
                else
                {
                    if (ulineitem.Substring(len_lineitem - 30, 5) == "ROC -")
                        rvalue = "Display ROC";
                    else
                        rvalue = "Display ROS";
                }
            }
            else if (ulineitem.Length > 12)
                if (ulineitem.Substring(8, 3) == " - ")
                {
                    if (ulineitem.IndexOf("- RON -") > 0)
                        rvalue = "Display RON";
                    else if (ulineitem.IndexOf("SPONS") >= 12 || ulineitem.IndexOf("ROS") >= 12)
                        rvalue = "Display ROS";
                }

            if (uorder.IndexOf("HEADER BIDDING") > 0)
                rvalue = "Header Bidding";

            if (ulineitem.IndexOf("REDUX - RTB") > 0)
                rvalue = "Display RTB";

            if (rvalue == "Not found")
            {
                int pos = ulineitem.IndexOf("- RO");
                if (pos > 0)
                {
                    if (lineItemTypes.ContainsKey("Display RO" + ulineitem.Substring(pos + 4, 1)))
                        rvalue = "Display RO" + ulineitem.Substring(pos + 4, 1);
                }
                else if (uall.IndexOf("REMNANTS") > 0)
                    rvalue = "Remnant";
                else if (ulineitem.StartsWith("HOUSE"))
                    rvalue = "Display ROS";
                else if (ulineitem.StartsWith("RON -"))
                    rvalue = "Display RON";
                else if (ulineitem.IndexOf("HOMEPAGE") > 0 ||
                    ulineitem.IndexOf("HOME PAGE") > 0 ||
                    ulineitem.IndexOf("ACCEUIL") > 0 ||
                    ulineitem.IndexOf("ACCUEIL") > 0 ||
                    uall.IndexOf("HOUSE") > 0 ||
                    ulineitem.IndexOf("WALLPAPER") > 0 ||
                    ulineitem.IndexOf("CATFISH") > 0 ||
                    ulineitem.IndexOf("POPUP") > 0 ||
                    ulineitem.IndexOf("ROS -") > 0 ||
                    ulineitem.IndexOf("SPONSORSHIP") > 0 ||
                    ulineitem.IndexOf("-BILLBOARD-") > 0 ||
                    ulineitem.IndexOf("-LEADERBOARD-") > 0)
                    rvalue = "Display ROS";
            }

            return lineItemTypes[rvalue];
        }
        #endregion

        #region dates
        public static string toMySqlDate(Google.Api.Ads.Dfp.v201508.DateTime dateTime)
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
        public static Google.Api.Ads.Dfp.v201508.Date toGoogleDate(System.DateTime dateTime)
        {
            Google.Api.Ads.Dfp.v201508.Date ret = new Google.Api.Ads.Dfp.v201508.Date();
            if(dateTime != null)
            {
                ret.year = dateTime.Year;
                ret.month = dateTime.Month;
                ret.day = dateTime.Day;
            }
            return ret;
        }
        public static bool DifferentDate(Db db, System.DateTime mySqlDateTime, Google.Api.Ads.Dfp.v201508.DateTime value)
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
        #endregion
        
        public static void createDuplicateCreative(Process process, long oldCreativeId)
        {
            process.log(" *** policyViolation");

            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();
 
            CreativeService creativeService = (CreativeService)interfaceUser.GetService(DfpService.v201508.CreativeService);
            StatementBuilder creativeStatementBuilder = new StatementBuilder()
                .Where("id = " + oldCreativeId);
            CreativePage creativePage = new CreativePage();

            LineItemCreativeAssociationService licaService = (LineItemCreativeAssociationService)interfaceUser.GetService(DfpService.v201508.LineItemCreativeAssociationService);
            StatementBuilder licaStatementBuilder = new StatementBuilder()
                .Where("creativeId = " + oldCreativeId);
            LineItemCreativeAssociationPage licaPage = new LineItemCreativeAssociationPage();
            List<LineItemCreativeAssociation> licas = new List<LineItemCreativeAssociation>();

            string lineitems = "";

            try
            {
                creativePage = creativeService.getCreativesByStatement(creativeStatementBuilder.ToStatement());
                Creative creative = creativePage.results[0];
                creative.id = 0;
                Creative[] createdCreatives = creativeService.createCreatives(new Creative[] {creative});
                process.log("New creativeId " + createdCreatives[0].id);

                licaPage = licaService.getLineItemCreativeAssociationsByStatement(licaStatementBuilder.ToStatement());
                foreach(LineItemCreativeAssociation lica in licaPage.results)
                {
                    lica.creativeId = createdCreatives[0].id;
                    lica.stats = null;
                    lineitems += ((lineitems == "") ? "" : ",") + lica.lineItemId;
                    process.log("associating with lineitemId " + lica.lineItemId);
                    licas.Add(lica);
                }
                licaService.createLineItemCreativeAssociations(licas.ToArray());

                string emailTo = "tcapiaccess@tc.tc";
                if(ConfigurationManager.AppSettings["creativePolicyViolations"] != null)
                    emailTo = ConfigurationManager.AppSettings["creativePolicyViolations"] + ",tcapiaccess@tc.tc";

                Db.sendMail(process, emailTo, "Creative disabled for policy Violation", "A new creative has been created.\nId: " + createdCreatives[0].id + "\nAnd associated with lineitems: " + lineitems);
            }
            catch(Exception ex)
            {
                Db.sendError(process, "Unable to duplicate creative " + oldCreativeId + "\n" + ex.Message);
            }



        }
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
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return ret;
        }
        public static Order getDeserializedOrder(string xmlString)
        {
            Order order = new Order();
            try
            {
                XmlSerializer x = new XmlSerializer(typeof(Order));
                order = (Order)x.Deserialize(new StringReader(xmlString.Replace("&apos;", "'")));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return order;
        }
        public static LineItem getDeserializedLineItem(string xmlString)
        {
            LineItem lineitem = new LineItem();
            try
            {
                XmlSerializer x = new XmlSerializer(typeof(LineItem));
                lineitem = (LineItem)x.Deserialize(new StringReader(xmlString.Replace("&apos;","'")));
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return lineitem;
        }

        public static string extractAppnexusId(string snippet)
        {
            string appnexusId = null;
            long numeric;

            int length = 1;
            try
            {
                while (long.TryParse(snippet.Substring(snippet.IndexOf("ttj?id=") + 7, length), out numeric))
                {
                    appnexusId = snippet.Substring(snippet.IndexOf("ttj?id=") + 7, length);
                    length++;
                }
                if (appnexusId == null)
                    while (long.TryParse(snippet.Substring(snippet.IndexOf("mob?id=") + 7, length), out numeric))
                    {
                        appnexusId = snippet.Substring(snippet.IndexOf("mob?id=") + 7, length);
                        length++;
                    }
            }
            catch (Exception) { } // If anything goes wrong, returns null.

            return appnexusId;
        }

        #region apply
        public static void applyLabel(Process process, Db db, string adUnitId, long companyId, long creativePlaceholderId, long lineItemId, long orderId, long creativeId, long labelId, bool isNegated, bool isEffective)
        {
            StringBuilder param = new StringBuilder();
            StringBuilder values = new StringBuilder();

            param.Append("labelId"); values.Append(labelId);
            if (adUnitId != Db.NULL) { param.Append(", adUnitId"); values.Append(",'" + adUnitId + "'"); }
            if (companyId != Db.NULLLONG) { param.Append(",companyId"); values.Append("," + companyId); }
            if (creativePlaceholderId != Db.NULLLONG) { param.Append(",creativePlaceholderId"); values.Append("," + creativePlaceholderId); }
            if (lineItemId != Db.NULLLONG) { param.Append(",lineItemId"); values.Append("," + lineItemId); }
            if (orderId != Db.NULLLONG) { param.Append(",orderId"); values.Append("," + orderId); }

            param.Append(",isNegated"); values.Append("," + ((isNegated) ? 1 : 0));
            param.Append(",isEffective"); values.Append("," + ((isEffective) ? 1 : 0));

            process.log(" ... New link");
            Db.execSqlCommand(process, "INSERT into dfp_labels_links (" + param.ToString() + ") VALUES (" + values.ToString() + ")");
            Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValue,Info,Data) VALUES('labelId'," + labelId + ",'Apply label','" + param.ToString() + "')");
        }
        public static void applyContact(Process process, Db db, long orderId, long contactId, string type)
        {
            StringBuilder param = new StringBuilder();
            StringBuilder values = new StringBuilder();

            param.Append("orderId"); values.Append(orderId);
            param.Append(",contactId"); values.Append("," + contactId);
            param.Append(",type"); values.Append(",'" + type + "'");

            process.log(" ... New link");
            Db.execSqlCommand(process, "INSERT into dfp_orders_contacts (" + param.ToString() + ") VALUES (" + values.ToString() + ")");
            Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValue,Info,Data) VALUES('orderId'," + orderId + ",'Apply contact','" + param.ToString() + "')");
        }
        public static void applySecondaryUser(Process process, Db db, long orderId, long userId, string type)
        {
            StringBuilder param = new StringBuilder();
            StringBuilder values = new StringBuilder();

            param.Append("orderId"); values.Append(orderId);
            param.Append(",userId"); values.Append("," + userId);
            param.Append(",type"); values.Append(",'" + type + "'");

            process.log(" ... New link");
            Db.execSqlCommand(process, "INSERT into dfp_orders_secondaryusers (" + param.ToString() + ") VALUES (" + values.ToString() + ")");
            //execSqlCommand(process, "INSERT INTO logs (ObjectName,ObjectValue,Info,Data) VALUES('orderId'," + orderId + ",'Apply secondaryUser','" + param.ToString() + "')");
        }
        public static void applyTeam(Process process, string adUnitId, long teamId, long adunitId, long companyId, long orderId, long userId)
        {
            StringBuilder param = new StringBuilder();
            StringBuilder values = new StringBuilder();

            param.Append("teamId"); values.Append(teamId);
            if (adUnitId != Db.NULL) { param.Append(", adUnitId"); values.Append(",'" + adUnitId + "'"); }
            if (adunitId != Db.NULLLONG) { param.Append(",adunitId"); values.Append("," + adunitId); }
            if (companyId != Db.NULLLONG) { param.Append(",companyId"); values.Append("," + companyId); }
            if (orderId != Db.NULLLONG) { param.Append(",orderId"); values.Append("," + orderId); }
            if (userId != Db.NULLLONG) { param.Append(",userId"); values.Append("," + userId); }

            process.log(" ... New link");
            Db.execSqlCommand(process, "INSERT into dfp_teams_links (" + param.ToString() + ") VALUES (" + values.ToString() + ")");
            Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValue,Info,Data) VALUES('teamId'," + teamId + ",'Apply Team','" + param.ToString() + "')");
        }
        public static void applyCreativePlaceholder(Process process, Db db, long lineItemId, CreativePlaceholder creativePlaceholder)
        {
            StringBuilder param = new StringBuilder();
            StringBuilder values = new StringBuilder();

            param.Append("lineItemId"); values.Append(lineItemId);
            if (creativePlaceholder.size != null)
            {
                param.Append(",size_width"); values.Append("," + creativePlaceholder.size.width);
                param.Append(",size_height"); values.Append("," + creativePlaceholder.size.height);
                param.Append(",size_isAspectRatio"); values.Append("," + ((creativePlaceholder.size.isAspectRatio) ? 1 : 0));
            }
            if (creativePlaceholder.companions != null) { param.Append(",hasCompanion"); values.Append(",1"); }

            process.log(" ... New link");
            Db.execSqlCommand(process, "INSERT into dfp_lineitems_creativeplaceholders (" + param.ToString() + ") VALUES (" + values.ToString() + ")");
            Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValue,Info,Data) VALUES('lineItemId'," + lineItemId + ",'Apply CreativePlaceholder','" + param.ToString() + "')");
        }
        public static void applyCreativePlaceholderCompanion(Process process, Db db, long lineItem_creativePlaceholderId, CreativePlaceholder companion)
        {
            StringBuilder param = new StringBuilder();
            StringBuilder values = new StringBuilder();

            param.Append("lineItem_creativePlaceholderId"); values.Append(lineItem_creativePlaceholderId);
            //param.Append(",lineItem_creativePlaceholder_companionId"); values.Append("," + companion.id);

            process.log(" ... New link");
            Db.execSqlCommand(process, "INSERT into dfp_lineitems_creativeplaceholders_companions (" + param.ToString() + ") VALUES (" + values.ToString() + ")");
            Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValue,Info,Data) VALUES('creativePlaceholderId'," + lineItem_creativePlaceholderId + ",'Apply CreativePlaceholder Companion','" + param.ToString() + "')");
        }
        public static void addProgrammaticSettings(Process process, Db db, Order order)
        {
            StringBuilder param = new StringBuilder();
            StringBuilder values = new StringBuilder();

            param.Append("orderId"); values.Append(order.id);

            if (order.programmaticSettings.adxBuyerNetworkIdSpecified) { param.Append(",adxBuyerNetworkId"); values.Append("," + order.programmaticSettings.adxBuyerNetworkId); }
            if (order.programmaticSettings.billingTermsTypeSpecified) { param.Append(",billingTermsType"); values.Append(",'" + order.programmaticSettings.billingTermsType + "'"); }
            if (order.programmaticSettings.buyerIdSpecified) { param.Append(",buyerId"); values.Append("," + order.programmaticSettings.buyerId); }
            if (order.programmaticSettings.buyerPlatformSpecified) { param.Append(",buyerPlatform"); values.Append(",'" + order.programmaticSettings.buyerPlatform + "'"); }
            if (order.programmaticSettings.statusSpecified) { param.Append(",status"); values.Append(",'" + order.programmaticSettings.status + "'"); }

            process.log(" ... New Programmatic");
            Db.execSqlCommand(process, "INSERT into dfp_orders_programmatic (" + param.ToString() + ") VALUES (" + values.ToString() + ")");
            Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValue,Info,Data) VALUES('orderId'," + order.id + ",'Add programmatic','" + param.ToString() + "')");
        }
        public static void addFrequencyCap(Process process, Db db, long lineItemId, FrequencyCap frequencyCap)
        {
            StringBuilder param = new StringBuilder();
            StringBuilder values = new StringBuilder();

            param.Append("lineItemId"); values.Append(lineItemId);

            if (frequencyCap.maxImpressionsSpecified) { param.Append(",maxImpressions"); values.Append("," + frequencyCap.maxImpressions); }
            if (frequencyCap.numTimeUnitsSpecified) { param.Append(",numTimeUnits"); values.Append("," + frequencyCap.numTimeUnits); }
            if (frequencyCap.timeUnitSpecified) { param.Append(",timeUnit"); values.Append(",'" + frequencyCap.timeUnit + "'"); }

            process.log(" ... New Frequency Cap");
            Db.execSqlCommand(process, "INSERT into dfp_lineitem_frequencycaps (" + param.ToString() + ") VALUES (" + values.ToString() + ")");
            Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValue,Info,Data) VALUES('lineItemId'," + lineItemId + ",'Add Frequency cap','" + param.ToString() + "')");
        }
        #endregion
    }
}
