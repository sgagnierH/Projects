using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.hearst.db;
using com.hearst.dfp;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201605;
using Google.Api.Ads.Dfp.v201605;
using Google.Api.Ads.Common.Util;
using System.Xml.Serialization;

namespace com.hearst.dfp
{
    class dfpLineitems : com.hearst.dfp.dfpBase
    {
        static void Main(string[] args)
        {
            dbBase.log("Starting");

            dfpLineitems run = new dfpLineitems();
            run.getPages(run, "dfp_lineitems", new Google.Api.Ads.Dfp.v201605.LineItemPage(), "lineitemId", args);

            Console.ReadKey();
        }
        public override dynamic getService(DfpUser user)
        {
            return user.GetService(DfpService.v201605.LineItemService);
        }
        public override dynamic getPage(dynamic service, StatementBuilder statementBuilder, int offset = StatementBuilder.SUGGESTED_PAGE_LIMIT)
        {
            return service.getLineItemsByStatement(statementBuilder.ToStatement());
        }
        public override void runCustomCode(dynamic item)
        {
            string tablename;
            #region inventoryTargetingc
            // Inventory targeting - lineitems
            tablename = "dfp_lineitems_targeting_inventorytargeting_adunits";
            dbBase.execSqlCommand("UPDATE " + tablename + " SET working='" + dbBase.guid + "' WHERE lineitemId=" + item.id);
            if (item.targeting.inventoryTargeting.targetedAdUnits != null)
                foreach (AdUnitTargeting adUnit in item.targeting.inventoryTargeting.targetedAdUnits)
                    dbBase.execSqlCommand("INSERT INTO " + tablename + " (lineitemId, isExcluded,adunitId) values(" + item.id + ",0," + adUnit.adUnitId + ") ON DUPLICATE KEY UPDATE working=null,isExcluded=0");
            if (item.targeting.inventoryTargeting.excludedAdUnits != null)
                foreach (AdUnitTargeting adUnit in item.targeting.inventoryTargeting.excludedAdUnits)
                    dbBase.execSqlCommand("INSERT INTO " + tablename + " (lineitemId, isExcluded,adunitId) values(" + item.id + ",1," + adUnit.adUnitId + ") ON DUPLICATE KEY UPDATE working=null,isExcluded=1");
            dbBase.execSqlCommand("DELETE FROM " + tablename + " WHERE working='" + dbBase.guid + "' AND lineitemId=" + item.id);

            tablename = "dfp_lineitems_targeting_inventorytargeting_placements";
            dbBase.execSqlCommand("UPDATE " + tablename + " SET working='" + dbBase.guid + "' WHERE lineitemId=" + item.id);
            if (item.targeting.inventoryTargeting.targetedPlacementIds != null)
                foreach (long id in item.targeting.inventoryTargeting.targetedPlacementIds)
                    dbBase.execSqlCommand("INSERT INTO " + tablename + " (lineitemId, placementId) values(" + item.id + "," + id + ") ON DUPLICATE KEY UPDATE working=null");
            dbBase.execSqlCommand("DELETE FROM " + tablename + " WHERE working='" + dbBase.guid + "' AND lineitemId=" + item.id);
            #endregion
        }
        public override void processCustomField(dynamic item, dynamic property, dynamic dataReader, ref string strSql, ref bool fieldProcessed, ref int i)
        {
            string fieldName = dataReader.GetName(i);
            dynamic value = (property == null) ? null :  property.GetValue(item, null);
            LineItem lineitem = (LineItem)item;

            #region serialised
            // Always save latest version
            if (!fieldProcessed && fieldName == "serialized")
            {
                strSql += ",serialized='" + dfpBase.getSerialized(item) + "'";
                fieldProcessed = true;
            }
            #endregion
            #region grpSettings
            if (!fieldProcessed && fieldName == "GrpSettings")
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
                    if ((string) dataReader["grpSettings_minTargetAge"].ToString() == "")
                        strSql += ",grpSettings_minTargetAge='" + lineitem.grpSettings.minTargetAge.ToString().Replace("'", "''") + "'";
                    else if (Convert.ToInt64(dataReader["grpSettings_minTargetAge"]) != lineitem.grpSettings.minTargetAge)
                        strSql += ",grpSettings_minTargetAge='" +lineitem.grpSettings.minTargetAge.ToString().Replace("'", "''") + "'";

                    if ((string)dataReader["grpSettings_maxTargetAge"].ToString() == "")
                        strSql += ",grpSettings_maxTargetAge='" + lineitem.grpSettings.maxTargetAge.ToString().Replace("'", "''") + "'";
                    else if (Convert.ToInt64(dataReader["grpSettings_maxTargetAge"]) != lineitem.grpSettings.maxTargetAge)
                        strSql += ",grpSettings_maxTargetAge='" + lineitem.grpSettings.maxTargetAge.ToString().Replace("'", "''") + "'";

                    if ((string)dataReader["grpSettings_minTargetAge"].ToString() == "")
                        strSql += ",grpSettings_minTargetAge='" + lineitem.grpSettings.targetGender.ToString().Replace("'", "''") + "'";
                    else if ("&" + dataReader["grpSettings_targetGender"].ToString() != "&" + lineitem.grpSettings.targetGender.ToString())
                        strSql += ",grpSettings_targetGender='" + lineitem.grpSettings.targetGender.ToString().Replace("'", "''") + "'";

                    if ((string)dataReader["grpSettings_provider"].ToString() == "")
                        strSql += ",grpSettings_provider='" + lineitem.grpSettings.provider.ToString().Replace("'", "''") + "'";
                    else if ("&" + dataReader["grpSettings_provider"].ToString() != "&" + lineitem.grpSettings.provider.ToString())
                        strSql += ",grpSettings_provider='" + lineitem.grpSettings.provider.ToString().Replace("'", "''") + "'";
                }
                i = i + 3; // skip all the 4 lines
                fieldProcessed = true;
            }
            #endregion
            #region stats
            if(!fieldProcessed && fieldName == "Stats")
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
                    if (Convert.ToInt64(dataReader["stats_clicksDelivered"]) != ((Google.Api.Ads.Dfp.v201605.Stats)value).clicksDelivered)
                        strSql += ",stats_clicksDelivered='" + ((Google.Api.Ads.Dfp.v201605.Stats)value).clicksDelivered.ToString().Replace("'", "''") + "'";

                    if (Convert.ToInt64(dataReader["stats_impressionsDelivered"]) != ((Google.Api.Ads.Dfp.v201605.Stats)value).impressionsDelivered)
                        strSql += ",stats_impressionsDelivered='" + ((Google.Api.Ads.Dfp.v201605.Stats)value).impressionsDelivered.ToString().Replace("'", "''") + "'";

                    if (Convert.ToInt64(dataReader["stats_videoCompletionsDelivered"]) != ((Google.Api.Ads.Dfp.v201605.Stats)value).videoCompletionsDelivered)
                        strSql += ",stats_videoCompletionsDelivered='" + ((Google.Api.Ads.Dfp.v201605.Stats)value).videoCompletionsDelivered.ToString().Replace("'", "''") + "'";

                    if (Convert.ToInt64(dataReader["stats_videoStartsDelivered"]) != ((Google.Api.Ads.Dfp.v201605.Stats)value).videoStartsDelivered)
                        strSql += ",stats_videoStartsDelivered='" + ((Google.Api.Ads.Dfp.v201605.Stats)value).videoStartsDelivered.ToString().Replace("'", "''") + "'";
                }
                i = i + 3; // skip all 4 lines
                fieldProcessed = true;
            }
            #endregion
            #region deliveryIndicator
            if (!fieldProcessed && fieldName == "DeliveryIndicator")
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
                    if (!dataReader["deliveryIndicator_actualDeliveryPercentage"].Equals(((DeliveryIndicator)value).actualDeliveryPercentage))
                        strSql += ",deliveryIndicator_actualDeliveryPercentage=" + ((DeliveryIndicator)value).actualDeliveryPercentage;

                    if (!dataReader["deliveryIndicator_expectedDeliveryPercentage"].Equals(((DeliveryIndicator)value).expectedDeliveryPercentage))
                        strSql += ",deliveryIndicator_expectedDeliveryPercentage=" + ((DeliveryIndicator)value).expectedDeliveryPercentage;
                }
                i = i + 1; // skip both lines
                fieldProcessed = true;
            }
            #endregion
            #region primaryGoal
            if (!fieldProcessed && fieldName == "Goal")
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
                    if (dataReader["primaryGoal_goalType"] == null)
                        strSql += ",primaryGoal_goalType='" + ((Google.Api.Ads.Dfp.v201605.Goal)value).goalType.ToString().Replace("'", "''") + "'";
                    else if ("&" + dataReader["primaryGoal_goalType"].ToString() != "&" + ((Google.Api.Ads.Dfp.v201605.Goal)value).goalType.ToString())
                        strSql += ",primaryGoal_goalType='" + ((Google.Api.Ads.Dfp.v201605.Goal)value).goalType.ToString().Replace("'", "''") + "'";

                    if (dataReader["primaryGoal_units"].ToString() == "")
                        strSql += ",primaryGoal_units=" + ((Google.Api.Ads.Dfp.v201605.Goal)value).units;
                    else if (Convert.ToInt64(dataReader["primaryGoal_units"]) != ((Google.Api.Ads.Dfp.v201605.Goal)value).units)
                        strSql += ",primaryGoal_units=" + ((Google.Api.Ads.Dfp.v201605.Goal)value).units;

                    if (dataReader["primaryGoal_unitType"].ToString() == "")
                        strSql += ",primaryGoal_unitType='" + ((Google.Api.Ads.Dfp.v201605.Goal)value).unitType.ToString().Replace("'", "''") + "'";
                    else if ("&" + dataReader["primaryGoal_unitType"].ToString() != "&" + ((Google.Api.Ads.Dfp.v201605.Goal)value).unitType.ToString())
                        strSql += ",primaryGoal_unitType='" + ((Google.Api.Ads.Dfp.v201605.Goal)value).unitType.ToString().Replace("'", "''") + "'";

                }
                i = i + 2; // skip all 3 lines about primaryGoal
                fieldProcessed = true;
            }
            #endregion
            #region creativePlaceholders
            if (!fieldProcessed && fieldName == "CreativePlaceholder[]")
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
                    else if (!dataReader.GetString(i).Equals(sizes))
                    {
                        strSql += ",creativePlaceholders_Sizes='" + sizes + "'";
                    }
                }
                fieldProcessed = true;
            }
            #endregion
        }
        public override dynamic getDeserialized(string xmlString)
        {
            LineItem lineitem = new LineItem();
            try
            {
                XmlSerializer x = new XmlSerializer(typeof(LineItem));
                lineitem = (LineItem)x.Deserialize(new StringReader(xmlString.Replace("&apos;", "'")));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return lineitem;
        }
    }
}
