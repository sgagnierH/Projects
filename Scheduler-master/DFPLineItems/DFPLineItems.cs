using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;
using Google.Api.Ads.Common.Util;
using Tc.TcMedia.Scheduler;
using Tc.TcMedia.Dfp;
using MySql.Data.MySqlClient;

namespace DFPLineItems
{
    public class DFPLineItems : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            string lastModifiedDateTime = "";
            lastModifiedDateTime = Db.getLastModifiedDateTime(process, "dfp_lineitems");
            process.log("Getting all LineItems" + ((lastModifiedDateTime == "") ? "" : " since " + lastModifiedDateTime));

            LineItemService lineItemService = (LineItemService)interfaceUser.GetService(DfpService.v201508.LineItemService);
            lineItemService.Timeout = Db.getTimeout();

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("lastModifiedDateTime ASC")
                .Where((lastModifiedDateTime == "") ? "" : "lastModifiedDateTime >= :lastModifiedDateTime")
                .AddValue("lastModifiedDateTime", lastModifiedDateTime)
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            int i = 0;
            LineItemPage page = new LineItemPage();
            do
            {
                int success = 0;
                int retries = 0;
                while(success == 0 && retries < 5)
                {
                    try
                    {
                        process.log("Loading...");
                        page = lineItemService.getLineItemsByStatement(statementBuilder.ToStatement());
                        process.log(" .. Loaded ");
                        success = 1;
                    }
                    catch (Exception ex)
                    {
                        retries++;
                        if (retries == 5)
                        {
                            process.log("Retry " + retries);
                            Db.sendError(process, ex.Message);
                        }
                    }
                }

                if (page.results != null && page.results.Length > 0)
                {
                    foreach (LineItem lineItem in page.results)
                    {
                        Console.WriteLine(i + "/" + page.totalResultSetSize + " : " + lineItem.id + " - " + lineItem.name);
                        string tablename;
                        #region targeting
                        #region contentTargeting
                        //if (lineItem.targeting.contentTargeting != null)
                        //{
                        //    tablename = "dfp_lineitems_targeting_contenttargeting";
                        //    foreach (long id in lineItem.targeting.contentTargeting.targetedContentIds)
                        //        Db.execSqlCommand(process, "INSERT INTO " + tablename + " (lineitemId, contentId, isExcluded) values(" + lineItem.id + "," + id + ",0) ON DUPLICATE KEY UPDATE working=null,isExcluded=0");
                        //    foreach (long id in lineItem.targeting.contentTargeting.excludedContentIds)
                        //        Db.execSqlCommand(process, "INSERT INTO " + tablename + " (lineitemId, contentId, isExcluded) values(" + lineItem.id + "," + id + ",1) ON DUPLICATE KEY UPDATE working=null,isExcluded=0");

                        //    tablename = "dfp_lineitems_targeting_customtargeting";
                        //    foreach (ContentMetadataKeyHierarchyTargeting cmkh in lineItem.targeting.contentTargeting.targetedContentMetadata)
                        //        Db.execSqlCommand(process, "INSERT INTO " + tablename + " (lineitemId, customTargetingValueId, isExcluded) values(" + lineItem.id + "," + cmkh.customTargetingValueIds + ",0) ON DUPLICATE KEY UPDATE working=null,isExcluded=0");
                        //    foreach (ContentMetadataKeyHierarchyTargeting cmkh in lineItem.targeting.contentTargeting.excludedContentMetadata)
                        //        Db.execSqlCommand(process, "INSERT INTO " + tablename + " (lineitemId, customTargetingValueId, isExcluded) values(" + lineItem.id + "," + cmkh.customTargetingValueIds + ",1) ON DUPLICATE KEY UPDATE working=null,isExcluded=0");

                        //    tablename = "dfp_lineitems_targeting_videocategories";
                        //    foreach (long videoId in lineItem.targeting.contentTargeting.targetedVideoCategoryIds)
                        //        Db.execSqlCommand(process, "INSERT INTO " + tablename + " (lineitemId, videoCategoryId, isExcluded) values(" + lineItem.id + "," + videoId + ",0) ON DUPLICATE KEY UPDATE working=null,isExcluded=0");
                        //    foreach (long videoId in lineItem.targeting.contentTargeting.excludedVideoCategoryIds)
                        //        Db.execSqlCommand(process, "INSERT INTO " + tablename + " (lineitemId, videoCategoryId, isExcluded) values(" + lineItem.id + "," + videoId + ",1) ON DUPLICATE KEY UPDATE working=null,isExcluded=0");

                        //    tablename = "dfp_lineitems_targeting_videocontentbundle";
                        //    foreach (long videoContentBundleId in lineItem.targeting.contentTargeting.targetedVideoContentBundleIds)
                        //        Db.execSqlCommand(process, "INSERT INTO " + tablename + " (lineitemId, videoCategoryId, isExcluded) values(" + lineItem.id + "," + videoContentBundleId + ",0) ON DUPLICATE KEY UPDATE working=null,isExcluded=0");
                        //    foreach (long videoContentBundleId in lineItem.targeting.contentTargeting.excludedVideoContentBundleIds)
                        //        Db.execSqlCommand(process, "INSERT INTO " + tablename + " (lineitemId, videoCategoryId, isExcluded) values(" + lineItem.id + "," + videoContentBundleId + ",1) ON DUPLICATE KEY UPDATE working=null,isExcluded=0");

                        //}
                        #endregion

                        #region customTargeting
                        //if(lineItem.targeting.customTargeting != null)
                        //{
                        //    foreach(CustomCriteriaNode node in lineItem.targeting.customTargeting.children)
                        //    {

                        //    }

                        //}
                        #endregion

                        #region dayPartTargeting
                        //if (lineItem.targeting.dayPartTargeting != null)
                        //{
                        //    foreach (DayPart daypart in lineItem.targeting.dayPartTargeting.dayParts)
                        //    {

                        //    }

                        //}
                        #endregion

                        #region geoTargeting
                        //tablename = "dfp_lineitems_targeting_geotargeting";
                        //Db.execSqlCommand(process, "UPDATE " + tablename + " SET working='" + process.guid + "' WHERE lineitemId=" + lineItem.id);
                        //if (lineItem.targeting.geoTargeting != null)
                        //{
                        //    if (lineItem.targeting.geoTargeting.targetedLocations != null)
                        //        foreach (Location location in lineItem.targeting.geoTargeting.targetedLocations)
                        //            Db.execSqlCommand(process, "INSERT INTO " + tablename + " (lineitemId, id, canonicalParentId, displayName, type, isExcluded) values(" + lineItem.id + "," + location.id + "," + location.canonicalParentId + ",'" + location.displayName.Replace("'", "''") + "','" + location.type + "',0 ) ON DUPLICATE KEY UPDATE canonicalParentId='" + location.canonicalParentId + "', displayName='" + location.displayName.Replace("'", "''") + "', type='" + location.type + "', isExcluded=0, working=null");
                        //    if (lineItem.targeting.geoTargeting.excludedLocations != null)
                        //        foreach (Location location in lineItem.targeting.geoTargeting.excludedLocations)
                        //            Db.execSqlCommand(process, "INSERT INTO " + tablename + " (lineitemId, id, canonicalParentId, displayName, type, isExcluded) values(" + lineItem.id + "," + location.id + "," + location.canonicalParentId + ",'" + location.displayName.Replace("'", "''") + "','" + location.type + "',1 ) ON DUPLICATE KEY UPDATE canonicalParentId='" + location.canonicalParentId + "', displayName='" + location.displayName.Replace("'", "''") + "', type='" + location.type + "', isExcluded=1, working=null");
                        //}
                        //Db.execSqlCommand(process, "DELETE FROM " + tablename + " WHERE working='" + process.guid + "' AND lineitemId=" + lineItem.id);
                        #endregion
                        
                        #region inventoryTargeting
                        // Inventory targeting - lineitems
                        tablename = "dfp_lineitems_targeting_inventorytargeting_adunits";
                        Db.execSqlCommand(process, "UPDATE " + tablename + " SET working='" + process.guid + "' WHERE lineitemId=" + lineItem.id);
                        if (lineItem.targeting.inventoryTargeting.targetedAdUnits != null)
                            foreach (AdUnitTargeting adUnit in lineItem.targeting.inventoryTargeting.targetedAdUnits)
                                Db.execSqlCommand(process, "INSERT INTO " + tablename + " (lineitemId, isExcluded,adunitId) values(" + lineItem.id + ",0," + adUnit.adUnitId + ") ON DUPLICATE KEY UPDATE working=null,isExcluded=0");
                        if (lineItem.targeting.inventoryTargeting.excludedAdUnits != null)
                            foreach (AdUnitTargeting adUnit in lineItem.targeting.inventoryTargeting.excludedAdUnits)
                                Db.execSqlCommand(process, "INSERT INTO " + tablename + " (lineitemId, isExcluded,adunitId) values(" + lineItem.id + ",1," + adUnit.adUnitId + ") ON DUPLICATE KEY UPDATE working=null,isExcluded=1");
                        Db.execSqlCommand(process, "DELETE FROM " + tablename + " WHERE working='" + process.guid + "' AND lineitemId=" + lineItem.id);

                        tablename = "dfp_lineitems_targeting_inventorytargeting_placements";
                        Db.execSqlCommand(process, "UPDATE " + tablename + " SET working='" + process.guid + "' WHERE lineitemId=" + lineItem.id);
                        if (lineItem.targeting.inventoryTargeting.targetedPlacementIds != null)
                            foreach (long id in lineItem.targeting.inventoryTargeting.targetedPlacementIds)
                                Db.execSqlCommand(process, "INSERT INTO " + tablename + " (lineitemId, placementId) values(" + lineItem.id + "," + id + ") ON DUPLICATE KEY UPDATE working=null");
                        Db.execSqlCommand(process, "DELETE FROM " + tablename + " WHERE working='" + process.guid + "' AND lineitemId=" + lineItem.id);
                        #endregion

                        #region technologyTargeting
                        //if (lineItem.targeting.technologyTargeting != null)
                        //{
                        //    TechnologyTargeting technologyTargeting = lineItem.targeting.technologyTargeting;

                        //}
                        #endregion

                        #region userDomaineTargeting
                        #endregion

                        #region videoPositionTargeting
                        #endregion
                        #endregion

                        process.log((++i + "/" + page.totalResultSetSize) + " : " + lineItem.id + " - " + lineItem.name);
                        DFPBase.checkDfpObject(process, "dfp_lineitems", "lineitemId", lineItem);
                    }
                }

                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            process.log("Number of items found: " + page.totalResultSetSize);
        }
    }
}
