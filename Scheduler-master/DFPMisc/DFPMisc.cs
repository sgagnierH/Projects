using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;
using Google.Api.Ads.Common.Util;
using Tc.TcMedia.Scheduler;
using Tc.TcMedia.Dfp;
using MySql.Data.MySqlClient;

namespace DFPMisc
{
    public class DFPMisc : iScheduler
    {
        
        public void Run(Process process)
        {
            DfpUser user = Tc.TcMedia.Dfp.Auth.getDfpUser();
            //getCustomFieldValues(process, user);
            //updateLineItems(process, user);
            //setCreatedLineItemsTypes(process, user);
            //updateCreatives(process, user);
            //updateCreativeWrappers(process, user);
            //updateProductTemplates(process, user);
            createPreBidLineItems(process, user);
            //updatePBCreatives(process, user);
            //fixLineItems(process, user);
            //editCreativeWrappers(process, user);
            //getProposalLineItems(process, user);
            Console.WriteLine("ok");
        }
        void getProposalLineItems(Process process, DfpUser user)
        {
            long proposalId = 191685;

            ProposalLineItemService service = (ProposalLineItemService)user.GetService(DfpService.v201508.ProposalLineItemService);
            ProposalLineItemPage page = new ProposalLineItemPage();
            StringBuilder sb = new StringBuilder();

            StatementBuilder stb = new StatementBuilder()
                .Where("proposalId=" + proposalId);

            page = service.getProposalLineItemsByStatement(stb.ToStatement());
            foreach (ProposalLineItem pli in page.results)
            {
                sb.Append(pli.id + ",'" + pli.name + "'\n");
            }
            Console.WriteLine(sb.ToString());
        }
        public void editCreativeWrappers(Process process, DfpUser user)
        {
            CreativeWrapperService service = (CreativeWrapperService)user.GetService(DfpService.v201508.CreativeWrapperService);
            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id ASC");

            CreativeWrapperPage page = new CreativeWrapperPage();
            List<CreativeWrapper> creativeWrappers = new List<CreativeWrapper>();
            page = service.getCreativeWrappersByStatement(statementBuilder.ToStatement());

            if (page.results != null && page.results.Length > 0)
            {
                foreach (CreativeWrapper creativeWrapper in page.results)
                {
                    if (creativeWrapper.status == CreativeWrapperStatus.ACTIVE)
                    {
                        if(creativeWrapper.header.htmlSnippet.IndexOf("tcAdInfo") == -1){
                            if (creativeWrapper.header.htmlSnippet != "")
                                creativeWrapper.header.htmlSnippet += "\n";
                            creativeWrapper.header.htmlSnippet += "<script>var tcAdInfo = {AdvertiserID : %eadv!,OrderID: %ebuy!,LineItemID: %eaid!,CreativeID: %ecid!,Geo: '%g',AdUnit1: '%s'};</script>";

                            creativeWrappers.Clear();
                            creativeWrappers.Add(creativeWrapper);
                            service.updateCreativeWrappers(creativeWrappers.ToArray());
                        }
                    }
                }
            }

        }
        public void fixLineItems(Process process, DfpUser user)
        {
            LineItemService lineItemService = (LineItemService)user.GetService(DfpService.v201508.LineItemService);
            lineItemService.Timeout = Db.getTimeout();

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id DESC")
                .Where("id <= 36513765")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            int i = 0;
            LineItemPage page = new LineItemPage();
            do
            {
                int success = 0;
                int retries = 0;
                while (success == 0 && retries < 5)
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
                        string tablename;
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

                        process.log((++i + "/" + page.totalResultSetSize) + " : " + lineItem.id + " - " + lineItem.name);
                        DFPBase.checkDfpObject(process, "dfp_lineitems", "lineitemId", lineItem);
                    }
                }

                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            process.log("Number of items found: " + page.totalResultSetSize);
        }
        void updatePBCreatives(Process process, DfpUser user)
        {
            int increment = 10; // 0.10$
            int start = 60;
            int end = 4500; // 45$
            long baseLineItemId = 251402325;
            string svalue = "";
            LineItemService lineItemService = (LineItemService)user.GetService(DfpService.v201508.LineItemService);
            LineItemPage lineItemPage = new LineItemPage();
            List<LineItem> lineItems = new List<LineItem>();
            List<LineItemCreativeAssociation> licas = new List<LineItemCreativeAssociation>();

            StringBuilder sb = new StringBuilder();

            #region lica
            // Get the creatives associated with this lineitem
            List<LineItemCreativeAssociation> baseLicas = new List<LineItemCreativeAssociation>();
            LineItemCreativeAssociationService licaService = (LineItemCreativeAssociationService)user.GetService(DfpService.v201508.LineItemCreativeAssociationService);
            LineItemCreativeAssociationPage licaPage = new LineItemCreativeAssociationPage();
            StatementBuilder licaBuilder = new StatementBuilder()
                .Where("lineitemId = " + baseLineItemId)
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            try
            {
                licaPage = licaService.getLineItemCreativeAssociationsByStatement(licaBuilder.ToStatement());
                if (licaPage.results != null && licaPage.results.Length > 0)
                    foreach (LineItemCreativeAssociation lica in licaPage.results)
                        baseLicas.Add(lica);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to retrieve LICAs;. Exception says \"{0}\"", e.Message);
                throw e;
            }
            #endregion

            for (int i = start; i <= end; i += increment)
            {
                try
                {
                    // Set lineitem Name
                    svalue = Math.Floor(Convert.ToDouble(i / 100)) + "." + i % 100;
                    if (i % 100 == 0) svalue += "0";
                    string extra = "_";
                    if (svalue.Length > 4) extra = "";

                    StatementBuilder lineItemStatementBuilder = new StatementBuilder()
                        .OrderBy("id ASC")
                        .Where("name = 'Prebid_" + extra + svalue + "_USD'")
                        .Limit(1);

                    lineItemPage = lineItemService.getLineItemsByStatement(lineItemStatementBuilder.ToStatement());
                    LineItem lineItem = lineItemPage.results[0];

                    StatementBuilder licaStatementBuilder = new StatementBuilder()
                      .Where("lineItemId = :lineItemId")
                      .OrderBy("lineItemId ASC, creativeId ASC")
                      .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT)
                      .AddValue("lineItemId", lineItem.id);

                    licaPage = licaService.getLineItemCreativeAssociationsByStatement(licaStatementBuilder.ToStatement());
                    if(licaPage.results != null)
                        if (licaPage.results.Length > 0)
                        {
                            licaStatementBuilder.RemoveLimitAndOffset();
                            DeleteLineItemCreativeAssociations action = new DeleteLineItemCreativeAssociations();
                            UpdateResult result = licaService.performLineItemCreativeAssociationAction(action, licaStatementBuilder.ToStatement());
                            if (result != null && result.numChanges > 0)
                            {
                                Console.WriteLine("Number of LICAs deactivated: {0}", result.numChanges);
                            }
                            else
                            {
                                Console.WriteLine("No LICAs were deactivated.");
                            }
                        }
                    // create Licas
                    licas.Clear();
                    foreach (LineItemCreativeAssociation lica in baseLicas)
                    {
                        lica.lineItemId = lineItem.id;
                        licas.Add(lica);
                    }
                    LineItemCreativeAssociation[] newLicas = licaService.createLineItemCreativeAssociations(licas.ToArray());

                }
                catch (Exception ex)
                {
                    process.log(ex.Message);
                }
            }

        }
        void createPreBidLineItems(Process process, DfpUser user)
        {
            int increment = 50; // 50 = 0.50$
            int start = 1000;  // 1.00$ = 100
            int end = 2000; // 50$ = 5000
            long baseLineItemId = 266186085; // Base lineitemId to duplicate
            long customTargetingKeyId = 300645; // Custom Key Targeting Id
            bool inUSD = false;

            long customTargetingValueId = 0;
            long newLineItemId = 0;
            string svalue = "";

            StringBuilder sb = new StringBuilder();

            #region lica
            // Get the creatives associated with this lineitem
            List<LineItemCreativeAssociation> baseLicas = new List<LineItemCreativeAssociation>();
            LineItemCreativeAssociationService licaService = (LineItemCreativeAssociationService)user.GetService(DfpService.v201508.LineItemCreativeAssociationService);
            LineItemCreativeAssociationPage licaPage = new LineItemCreativeAssociationPage();
            StatementBuilder licaBuilder = new StatementBuilder()
                .Where("lineitemId = " + baseLineItemId)
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            try
            {
                licaPage = licaService.getLineItemCreativeAssociationsByStatement(licaBuilder.ToStatement());
                if (licaPage.results != null && licaPage.results.Length > 0)
                    foreach (LineItemCreativeAssociation lica in licaPage.results)
                        baseLicas.Add(lica);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to retrieve LICAs;. Exception says \"{0}\"", e.Message);
                throw e;
            }
            #endregion
            #region retrieve Base lineItem
            LineItemService lineItemService = (LineItemService)user.GetService(DfpService.v201508.LineItemService);
            LineItemPage lineItemPage = new LineItemPage();
            StatementBuilder lineItemStatementBuilder = new StatementBuilder()
                .OrderBy("id ASC")
                .Where("id = " +  baseLineItemId)
                .Limit(1);

            lineItemPage = lineItemService.getLineItemsByStatement(lineItemStatementBuilder.ToStatement());
            LineItem baseLineItem = lineItemPage.results[0];
            #endregion

            for (int i = start; i <= end; i += increment)
            {
                try
                {
                    List<LineItem> lineItems = new List<LineItem>();

                    // Reset Id
                    baseLineItem.id = -1;

                    // Set lineitem Name
                    svalue = Math.Floor(Convert.ToDouble(i / 100)) + "." + i % 100;
                    if (i % 100 == 0) svalue += "0";
                    string extra = "_";
                    if (svalue.Length > 4) extra = "";

                    baseLineItem.name = "Prebid_" + extra + svalue + ((inUSD) ? "_USD" : "");
                    process.log(baseLineItem.name);
                    baseLineItem.skipInventoryCheck = true;
                    baseLineItem.startDateTimeType = StartDateTimeType.IMMEDIATELY;

                    // set costPreUnit
                    baseLineItem.costPerUnit.microAmount = i * 10000;

                    #region targeting value
                    // Create targeting value
                    CustomTargetingService customTargetingService = (CustomTargetingService)user.GetService(DfpService.v201508.CustomTargetingService);
                    CustomTargetingValue currentValue = new CustomTargetingValue();
                    CustomTargetingValuePage ctPage = new CustomTargetingValuePage();

                    StatementBuilder statementBuilder = new StatementBuilder()
                        .Where("customTargetingKeyId = :customTargetingKeyId")
                        .OrderBy("id DESC")
                        .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT)
                        .AddValue("customTargetingKeyId", customTargetingKeyId);

                    try
                    {
                        ctPage = customTargetingService.getCustomTargetingValuesByStatement(statementBuilder.ToStatement());
                        bool found = false;
                        if (ctPage.results != null)
                        {
                            for (int ii = 0; ii < ctPage.results.Length && !found; ii++)
                            {
                                CustomTargetingValue customTargeting = (CustomTargetingValue)ctPage.results[ii];
                                if (customTargeting.name == svalue)
                                {
                                    customTargetingValueId = ctPage.results[ii].id;
                                    found = true;
                                }
                            }
                        }
                        if(!found)
                        {
                            currentValue.customTargetingKeyId = customTargetingKeyId;
                            currentValue.name = svalue;
                            currentValue.matchType = CustomTargetingValueMatchType.EXACT;
                            CustomTargetingValue[] returnValues = customTargetingService.createCustomTargetingValues(new CustomTargetingValue[] { currentValue });
                            customTargetingValueId = returnValues[0].id;
                        }
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }

                    // set targeting
                    for (int jj = 0; jj < ((CustomCriteriaSet)baseLineItem.targeting.customTargeting.children[0]).children.Length; jj++ )
                    {
                        CustomCriteria customCriteria = (CustomCriteria)((CustomCriteriaSet)baseLineItem.targeting.customTargeting.children[0]).children[jj];
                        Console.Write("ok");
                        if (customCriteria.keyId == customTargetingKeyId)
                            customCriteria.valueIds[0] = customTargetingValueId;
                    }
                    //((Google.Api.Ads.Dfp.v201508.CustomCriteria)(((Google.Api.Ads.Dfp.v201508.CustomCriteriaSet)(baseLineItem.targeting.customTargeting.children[0])).children[0])).valueIds[0] = customTargetingValueId;
                    #endregion

                    // Add the new lineitem
                    lineItems.Add(baseLineItem);
                    LineItem[] newLineItems = lineItemService.createLineItems(lineItems.ToArray());

                    // Recover new id
                    newLineItemId = newLineItems[0].id;

                    // create Licas
                    List<LineItemCreativeAssociation> licas = new List<LineItemCreativeAssociation>();
                    foreach (LineItemCreativeAssociation lica in baseLicas)
                    {
                        lica.lineItemId = newLineItemId;
                        lica.startDateTimeType = StartDateTimeType.IMMEDIATELY;
                        licas.Add(lica);
                    }
                    LineItemCreativeAssociation[] newLicas = licaService.createLineItemCreativeAssociations(licas.ToArray());

                }
                catch(Exception ex)
                {
                    process.log(ex.Message);
                }
            }
        }
        void setCreatedLineItemsTypes(Process process, DfpUser user)
        {
            StringBuilder sb = new StringBuilder();
            LineItemService lineItemService = (LineItemService)user.GetService(DfpService.v201508.LineItemService);
            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id DESC")
                .Where("isArchived = false AND (endDateTime is null OR startDateTime > :now OR (startDateTime < :now AND endDateTime > :now))")
                .AddValue("now", System.DateTime.Now.AddDays(3).ToString("yyyy-MM-ddThh:mm:ss"))
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            int i = 0;
            LineItemPage page = new LineItemPage();
            sb.AppendLine("'orderId','orderName','lineItemId','lineItemName'");
            try
            {
                do
                {
                    int success = 0;
                    int retries = 0;
                    while (success == 0 && retries < 5)
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
                            ++i;
                            string ret = DFPBase.checkLineItemType(process, lineItem, lineItemService);
                            process.log(i + "/" + page.totalResultSetSize + " : " + lineItem.orderId + " " + lineItem.orderName + " : " + lineItem.id + " " + lineItem.name + " " + ret);
                            if (ret == "Not found")
                            {
                                sb.AppendLine(lineItem.orderId + ",'" + lineItem.orderName + "'," + lineItem.id + ",'" + lineItem.name + "'");
                            }
                        }
                    }
                    statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
                } while (statementBuilder.GetOffset() < page.totalResultSetSize);

                process.log("Number of items found: " + page.totalResultSetSize);
            }
            catch (Exception ex)
            {
                sb.AppendLine("\n-------------\nERREURS\n-------------\n" + ex.Message);
                throw ex;
            }
            finally
            {
                System.IO.File.WriteAllText("C:\\temp\\lineItemFix.res", sb.ToString());
            }
        }
        void getCustomFieldValues(Process process, DfpUser user)
        {
            Dictionary<long, long> lineItemTypes = new Dictionary<long, long>();
            StringBuilder sb = new StringBuilder();
            //LineItemService service = (LineItemService)user.GetService(DfpService.v201508.LineItemService);
            //LineItemPage page = new LineItemPage();
            OrderService service = (OrderService)user.GetService(DfpService.v201508.OrderService);
            OrderPage page = new OrderPage();

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("lastModifiedDateTime DESC")
                .Where("id = 304962405");

            try
            {
                do
                {
                    page = service.getOrdersByStatement(statementBuilder.ToStatement());
                    if (page.results != null && page.results.Length > 0)
                    {
                        foreach (Order order in page.results)//LineItem lineItem in page.results)
                        {
                            if (order.customFieldValues != null)
                            {
                                for(int i = 0; i < order.customFieldValues.Count(); i++)
                                {
                                    //process.log(lineItem.id + " : " + lineItem.customFieldValues[i].customFieldId);
                                    if(order.customFieldValues[i].customFieldId == 10245)
                                    { // 9525 = LineItem Type
                                        // 10245 = Sales Team ===> Redux = 30765
                                       DropDownCustomFieldValue customField = (DropDownCustomFieldValue)order.customFieldValues[i];
                                        if(!lineItemTypes.Keys.Contains(customField.customFieldOptionId))
                                        {
                                            lineItemTypes.Add(customField.customFieldOptionId, order.id);
                                            sb.AppendLine(order.id + "," + customField.customFieldOptionId.ToString());
                                        }
                                    }
                                }
                            }
                        }
                    }
                    statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
                } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            }
            catch(Exception ex)
            {
                sb.AppendLine("\n-------------\nERREURS\n-------------\n" + ex.Message);
                throw ex;
            }
            finally
            {
                System.IO.File.WriteAllText("C:\\temp\\lineItemTypes.res", sb.ToString());
            }
        }
        void updateLineItems(Process process, DfpUser user)
        {
            StringBuilder sb = new StringBuilder();
            string orders;
            Dictionary<long, LineItem> lineItems = new Dictionary<long, LineItem>();
            Dictionary<long, long> lineItemTypes = new Dictionary<long, long>();
            //lineItemTypes.Add(31461765, 28965); //'Google - Remnant' : 'Resold'
            //lineItemTypes.Add(32097765, 29085); //'AppNexus - Remnant' : 'Remnants'
            //lineItemTypes.Add(75268965, 29085); //'Rubicon - Remnants' : 'Remnants'

            foreach(long advertiserId in lineItemTypes.Keys)
            {
                sb.AppendLine("Advertiser : " + advertiserId + " --> " + lineItemTypes[advertiserId]);
                sb.AppendLine("OrderId,LineItemId,LineItemType");

                #region get Orders
                OrderService orderService = (OrderService)user.GetService(DfpService.v201508.OrderService);
                OrderPage orderPage = new OrderPage();
                StatementBuilder orderStatementBuilder = new StatementBuilder()
                    .OrderBy("id ASC")
                    .Where("id=339887325")
                    //.Where("advertiserId=" + advertiserId)
                    .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

                orders = "";
                lineItems.Clear();
                try
                {
                    do
                    {
                        orderPage = orderService.getOrdersByStatement(orderStatementBuilder.ToStatement());
                        if (orderPage.results != null && orderPage.results.Length > 0)
                        {
                            foreach (Order order in orderPage.results)
                            {
                                if(!order.isArchived)
                                {
                                    process.log("AdvertiserId : " + advertiserId + " - " + order.id + " " + order.name);
                                    orders = orders + ((orders == "") ? "" : ", ") + order.id;
                                }
                            }
                        }
                        orderStatementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
                    } while (orderStatementBuilder.GetOffset() < orderPage.totalResultSetSize);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Can't get orders for " + advertiserId + ". Exception says " + e.Message);
                }
                #endregion
                #region get LineItems
                LineItemService lineItemService = (LineItemService)user.GetService(DfpService.v201508.LineItemService);
                LineItemPage lineItemPage = new LineItemPage();

                StatementBuilder lineItemStatementBuilder = new StatementBuilder()
                    .OrderBy("id ASC")
                    .Where("orderId in (" + orders + ")")
                    .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

                bool found;
                try
                {
                    do
                    {
                        lineItemPage = lineItemService.getLineItemsByStatement(lineItemStatementBuilder.ToStatement());
                        if (lineItemPage.results != null && lineItemPage.results.Length > 0)
                        {
                            foreach (LineItem lineItem in lineItemPage.results)
                            {
                                if (!lineItem.isArchived)
                                {
                                    if (lineItem.costPerUnit.currencyCode != "USD")
                                    {
                                        lineItem.name = lineItem.name + "_USD";
                                        lineItem.costPerUnit.currencyCode = "USD";
                                        lineItems.Clear();
                                        lineItems.Add(lineItem.id, lineItem);
                                        try
                                        {
                                            lineItemService.updateLineItems(lineItems.Values.ToArray());
                                            sb.AppendLine(lineItem.orderId + "," + lineItem.id + "," + lineItemTypes[advertiserId]);
                                        }
                                        catch (Exception ex)
                                        {
                                            sb.AppendLine(lineItem.orderId + "," + lineItem.id + "," + lineItemTypes[advertiserId] + " ERREUR: " + ex.Message);
                                            process.log(ex.Message);
                                        }
                                    }
                                    //found = false;
                                    //List<BaseCustomFieldValue> customFieldValues = new List<BaseCustomFieldValue>();

                                    //// Transferring old custom fields
                                    //if (lineItem.customFieldValues != null)
                                    //{
                                    //    for (int i = 0; i < lineItem.customFieldValues.Length; i++)
                                    //    {
                                    //        if (lineItem.customFieldValues[i].customFieldId == 9525)
                                    //            found = true;
                                    //        else
                                    //            customFieldValues.Add(lineItem.customFieldValues[i]);
                                    //    }
                                    //}

                                    //if (!found)
                                    //{
                                    //    process.log(lineItem.orderId + " - " + lineItem.id + " " + lineItemTypes[advertiserId]);

                                    //    // Adding the new one
                                    //    DropDownCustomFieldValue customFieldValue = new DropDownCustomFieldValue();
                                    //    customFieldValue.customFieldId = 9525;
                                    //    customFieldValue.customFieldOptionId = lineItemTypes[advertiserId];
                                    //    customFieldValues.Add(customFieldValue);

                                    //    lineItem.customFieldValues = customFieldValues.ToArray();
                                    //    lineItems.Clear();
                                    //    lineItems.Add(lineItem.id, lineItem);

                                    //    try
                                    //    {
                                    //        lineItemService.updateLineItems(lineItems.Values.ToArray());
                                    //        sb.AppendLine(lineItem.orderId + "," + lineItem.id + "," + lineItemTypes[advertiserId]);
                                    //    }
                                    //    catch(Exception ex)
                                    //    {
                                    //        sb.AppendLine(lineItem.orderId + "," + lineItem.id + "," + lineItemTypes[advertiserId] + " ERREUR: " + ex.Message);
                                    //        process.log(ex.Message);
                                    //    }
                                    //}
                                }
                            }
                        }
                        lineItemStatementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
                    } while (lineItemStatementBuilder.GetOffset() < lineItemPage.totalResultSetSize);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Can't get orders for " + advertiserId + ". Exception says " + e.Message);
                }
                sb.AppendLine("-------");
                #endregion
            }
            System.IO.File.WriteAllText("C:\\temp\\lineItems.res", sb.ToString());
        }
        void updateCreatives(Process process, DfpUser user)
        {
            StringBuilder sb = new StringBuilder();
            Dictionary<long, Creative> creatives = new Dictionary<long, Creative>();

            CreativeService creativeService = (CreativeService)user.GetService(DfpService.v201508.CreativeService);
            CreativePage page = new CreativePage();

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id ASC")
                .AddValue("creativeType", "CustomCreative")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            try
            {
                page = creativeService.getCreativesByStatement(statementBuilder.ToStatement());
                if (page.results != null && page.results.Length > 0)
                {
                    foreach (CustomCreative creative in page.results)
                    {
                        if (creative.htmlSnippet.IndexOf("http://") > 0)
                        {
                            sb.Append(creative.id + " :be: " + creative.htmlSnippet.Replace("\n", "") + "\n");
                            creative.htmlSnippet = creative.htmlSnippet.Replace("\n", "").Replace("http://", "//");
                            sb.Append(creative.id + " :af: " + creative.htmlSnippet + "\n");
                            creatives.Add(creative.id, creative);
                            process.log(creative.id + " - ** updated");
                        }
                        else
                            process.log(creative.id + " - no http");
                    }
                    creativeService.updateCreatives(creatives.Values.ToArray());
                    process.log("Updating DFP : " + creatives.Count + " items");
                    System.IO.File.WriteAllText("C:\\temp\\creatives.res", sb.ToString());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to update creative wrappers. Exception says \"{0}\"",
                    e.Message);
            }

        }
        void updateCreativeWrappers(Process process, DfpUser user)
        {
            StringBuilder sb = new StringBuilder();
            Dictionary<long, CreativeWrapper> creativeWrappers = new Dictionary<long, CreativeWrapper>();

            CreativeWrapperService creativeWrapperService = (CreativeWrapperService)user.GetService(DfpService.v201508.CreativeWrapperService);
            CreativeWrapperPage page = new CreativeWrapperPage();

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id ASC")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            try
            {
                page = creativeWrapperService.getCreativeWrappersByStatement(statementBuilder.ToStatement());
                if (page.results != null && page.results.Length > 0)
                {
                    foreach (CreativeWrapper wrapper in page.results)
                    {
                        if (wrapper.header.htmlSnippet.IndexOf("http://") > 0)
                        {
                            sb.Append(wrapper.id + " :be: " + wrapper.header.htmlSnippet.Replace("\n", "") + "\n");
                            wrapper.header.htmlSnippet = wrapper.header.htmlSnippet.Replace("\n", "").Replace("http://", "//");
                            sb.Append(wrapper.id + " :af: " + wrapper.header.htmlSnippet + "\n");
                            creativeWrappers.Add(wrapper.id, wrapper);
                            process.log(wrapper.id + " - ** updated");
                        }
                        else
                            process.log(wrapper.id + " - no http");
                    }
                    creativeWrapperService.updateCreativeWrappers(creativeWrappers.Values.ToArray());
                    process.log("Updating DFP : " + creativeWrappers.Count + " items");
                    System.IO.File.WriteAllText("C:\\temp\\creativeWrappers.res", sb.ToString());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to update creative wrappers. Exception says \"{0}\"",
                    e.Message);
            }

        }
        void updateProductTemplates(Process process, DfpUser user)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder er = new StringBuilder();
            Dictionary<long, string> options = new Dictionary<long, string>();
            options.Add(27285, "Display - ROC");
            options.Add(27405, "Display - RON");
            options.Add(28245, "Display - RON - Audience");
            options.Add(28125, "Display - RON - Retargeting");
            options.Add(27525, "Display - ROS");
            options.Add(27645, "Display - RTB");
            options.Add(27885, "ePush");
            options.Add(28005, "Newsletters");
            options.Add(27765, "NSBD");

            Dictionary<long, long> lineItemTypes = defineProductLineItemTypes();
            Dictionary<long, ProductTemplate> productTemplates = new Dictionary<long, ProductTemplate>();

            ProductTemplateService productTemplateService = (ProductTemplateService)user.GetService(DfpService.v201508.ProductTemplateService);
            ProductTemplatePage page = new ProductTemplatePage();

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id ASC")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            int nb = 0;
            try
            {
                do 
                {
                    page = productTemplateService.getProductTemplatesByStatement(statementBuilder.ToStatement());
                    if (page.results != null && page.results.Length > 0)
                    {
                        foreach (ProductTemplate productTemplate in page.results)
                        {
                            if (lineItemTypes.Keys.Contains(productTemplate.id) && productTemplate.customFieldValues == null)
                            {
                                DropDownCustomFieldValue field = new DropDownCustomFieldValue();
                                field.customFieldId = 9645;
                                field.customFieldOptionId = lineItemTypes[productTemplate.id];
                                BaseCustomFieldValue[] fieldvalues = { field };
                                productTemplate.customFieldValues = fieldvalues;

                                productTemplates.Clear();
                                productTemplates.Add(productTemplate.id, productTemplate);

                                process.log(++nb + "," + productTemplate.id + "," + productTemplate.name + "," + field.customFieldId + "," + lineItemTypes[productTemplate.id] + "," + options[lineItemTypes[productTemplate.id]]);
                                try
                                { 
                                    productTemplateService.updateProductTemplates(productTemplates.Values.ToArray());
                                    sb.AppendLine(nb + "," + productTemplate.id + "," + productTemplate.name + "," + field.customFieldId + "," + lineItemTypes[productTemplate.id] + "," + options[lineItemTypes[productTemplate.id]]);
                                }
                                catch(Exception ex)
                                {
                                    process.log("Erreur - " + ex.Message);
                                    er.AppendLine(nb + "," + productTemplate.id + "," + productTemplate.name + "," + field.customFieldId + "," + lineItemTypes[productTemplate.id] + "," + options[lineItemTypes[productTemplate.id]] + "," + ex.Message);
                                }
                            }
                        }
                    }
                    statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
                } while (statementBuilder.GetOffset() < page.totalResultSetSize);
                
                System.IO.File.WriteAllText("C:\\temp\\productTemplates.res", sb.ToString() + "\n-------------\nERREURS\n-------------\n" + er.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to update product templates. Exception says \"{0}\"", e.Message);
            }
        }
        Dictionary<long, long> defineProductLineItemTypes()
        {
            Dictionary<long, long> ret = new Dictionary<long, long>();
            ret.Add(122325, 27285);
            ret.Add(105285, 27285);
            ret.Add(79365, 27285);
            ret.Add(85245, 27285);
            ret.Add(84885, 27285);
            ret.Add(79485, 27285);
            ret.Add(85125, 27285);
            ret.Add(88005, 27285);
            ret.Add(122205, 27285);
            ret.Add(86205, 27285);
            ret.Add(86325, 27285);
            ret.Add(85845, 27285);
            ret.Add(85965, 27285);
            ret.Add(83925, 27405);
            ret.Add(525, 27405);
            ret.Add(122445, 27405);
            ret.Add(92445, 27405);
            ret.Add(85485, 27405);
            ret.Add(645, 27405);
            ret.Add(79845, 27405);
            ret.Add(121725, 27405);
            ret.Add(76365, 27405);
            ret.Add(87885, 27405);
            ret.Add(80325, 27405);
            ret.Add(17205, 28245);
            ret.Add(18045, 28245);
            ret.Add(19005, 28245);
            ret.Add(19605, 28245);
            ret.Add(21885, 28245);
            ret.Add(22005, 28245);
            ret.Add(22245, 28245);
            ret.Add(22485, 28245);
            ret.Add(22845, 28245);
            ret.Add(22965, 28245);
            ret.Add(23085, 28245);
            ret.Add(73485, 28245);
            ret.Add(22125, 28245);
            ret.Add(83685, 28245);
            ret.Add(83805, 28245);
            ret.Add(82245, 28125);
            ret.Add(82365, 28125);
            ret.Add(82485, 28125);
            ret.Add(82605, 28125);
            ret.Add(77085, 27525);
            ret.Add(1365, 27525);
            ret.Add(1485, 27525);
            ret.Add(7845, 27525);
            ret.Add(100125, 27525);
            ret.Add(7725, 27525);
            ret.Add(7605, 27525);
            ret.Add(5925, 27525);
            ret.Add(12525, 27525);
            ret.Add(29685, 27525);
            ret.Add(5565, 27525);
            ret.Add(94365, 27525);
            ret.Add(94605, 27525);
            ret.Add(94485, 27525);
            ret.Add(6165, 27525);
            ret.Add(5805, 27525);
            ret.Add(123405, 27525);
            ret.Add(12645, 27525);
            ret.Add(6045, 27525);
            ret.Add(29805, 27525);
            ret.Add(5445, 27525);
            ret.Add(38805, 27525);
            ret.Add(43365, 27525);
            ret.Add(5685, 27525);
            ret.Add(124245, 27525);
            ret.Add(765, 27525);
            ret.Add(100005, 27525);
            ret.Add(23445, 27525);
            ret.Add(107805, 27525);
            ret.Add(24045, 27525);
            ret.Add(99885, 27525);
            ret.Add(24165, 27525);
            ret.Add(23685, 27525);
            ret.Add(23565, 27525);
            ret.Add(23805, 27525);
            ret.Add(39045, 27525);
            ret.Add(43485, 27525);
            ret.Add(4845, 27525);
            ret.Add(27645, 27525);
            ret.Add(4725, 27525);
            ret.Add(37365, 27525);
            ret.Add(27885, 27525);
            ret.Add(12765, 27525);
            ret.Add(37485, 27525);
            ret.Add(5325, 27525);
            ret.Add(4965, 27525);
            ret.Add(122805, 27525);
            ret.Add(5205, 27525);
            ret.Add(11325, 27525);
            ret.Add(27765, 27525);
            ret.Add(4605, 27525);
            ret.Add(37125, 27525);
            ret.Add(43725, 27525);
            ret.Add(5085, 27525);
            ret.Add(11445, 27525);
            ret.Add(29925, 27525);
            ret.Add(124005, 27525);
            ret.Add(124125, 27525);
            ret.Add(12885, 27525);
            ret.Add(13005, 27525);
            ret.Add(39165, 27525);
            ret.Add(45645, 27525);
            ret.Add(6525, 27525);
            ret.Add(30045, 27525);
            ret.Add(30165, 27525);
            ret.Add(6405, 27525);
            ret.Add(6765, 27525);
            ret.Add(6885, 27525);
            ret.Add(6285, 27525);
            ret.Add(43605, 27525);
            ret.Add(6645, 27525);
            ret.Add(30285, 27525);
            ret.Add(7245, 27525);
            ret.Add(13125, 27525);
            ret.Add(13245, 27525);
            ret.Add(7125, 27525);
            ret.Add(37725, 27525);
            ret.Add(7365, 27525);
            ret.Add(11205, 27525);
            ret.Add(13365, 27525);
            ret.Add(30405, 27525);
            ret.Add(7005, 27525);
            ret.Add(37605, 27525);
            ret.Add(43845, 27525);
            ret.Add(7485, 27525);
            ret.Add(32685, 27525);
            ret.Add(98325, 27525);
            ret.Add(98445, 27525);
            ret.Add(98565, 27525);
            ret.Add(11805, 27525);
            ret.Add(11685, 27525);
            ret.Add(11925, 27525);
            ret.Add(12165, 27525);
            ret.Add(11565, 27525);
            ret.Add(39285, 27525);
            ret.Add(43965, 27525);
            ret.Add(12045, 27525);
            ret.Add(23925, 27525);
            ret.Add(13485, 27525);
            ret.Add(13605, 27525);
            ret.Add(103845, 27525);
            ret.Add(103965, 27525);
            ret.Add(103725, 27525);
            ret.Add(13845, 27525);
            ret.Add(13725, 27525);
            ret.Add(13965, 27525);
            ret.Add(12405, 27525);
            ret.Add(32805, 27525);
            ret.Add(12285, 27525);
            ret.Add(81525, 27525);
            ret.Add(14085, 27525);
            ret.Add(39405, 27525);
            ret.Add(44085, 27525);
            ret.Add(14205, 27525);
            ret.Add(32925, 27525);
            ret.Add(14325, 27525);
            ret.Add(97245, 27525);
            ret.Add(97365, 27525);
            ret.Add(97485, 27525);
            ret.Add(14445, 27525);
            ret.Add(39525, 27525);
            ret.Add(44205, 27525);
            ret.Add(15165, 27525);
            ret.Add(33045, 27525);
            ret.Add(15285, 27525);
            ret.Add(97605, 27525);
            ret.Add(97725, 27525);
            ret.Add(97845, 27525);
            ret.Add(15045, 27525);
            ret.Add(15405, 27525);
            ret.Add(33165, 27525);
            ret.Add(15525, 27525);
            ret.Add(97965, 27525);
            ret.Add(98085, 27525);
            ret.Add(98205, 27525);
            ret.Add(15645, 27525);
            ret.Add(39645, 27525);
            ret.Add(44325, 27525);
            ret.Add(15765, 27525);
            ret.Add(33285, 27525);
            ret.Add(15885, 27525);
            ret.Add(95565, 27525);
            ret.Add(95445, 27525);
            ret.Add(95685, 27525);
            ret.Add(16005, 27525);
            ret.Add(39765, 27525);
            ret.Add(44445, 27525);
            ret.Add(16125, 27525);
            ret.Add(16245, 27525);
            ret.Add(16365, 27525);
            ret.Add(33405, 27525);
            ret.Add(95925, 27525);
            ret.Add(96045, 27525);
            ret.Add(95805, 27525);
            ret.Add(39885, 27525);
            ret.Add(44565, 27525);
            ret.Add(16485, 27525);
            ret.Add(33525, 27525);
            ret.Add(16605, 27525);
            ret.Add(96165, 27525);
            ret.Add(96285, 27525);
            ret.Add(96405, 27525);
            ret.Add(16725, 27525);
            ret.Add(40005, 27525);
            ret.Add(16845, 27525);
            ret.Add(33645, 27525);
            ret.Add(16965, 27525);
            ret.Add(96525, 27525);
            ret.Add(96645, 27525);
            ret.Add(96765, 27525);
            ret.Add(17085, 27525);
            ret.Add(40125, 27525);
            ret.Add(44685, 27525);
            ret.Add(24645, 27525);
            ret.Add(46125, 27525);
            ret.Add(24285, 27525);
            ret.Add(25125, 27525);
            ret.Add(25605, 27525);
            ret.Add(26085, 27525);
            ret.Add(45765, 27525);
            ret.Add(26325, 27525);
            ret.Add(120765, 27525);
            ret.Add(24405, 27525);
            ret.Add(26205, 27525);
            ret.Add(26565, 27525);
            ret.Add(26685, 27525);
            ret.Add(45885, 27525);
            ret.Add(26805, 27525);
            ret.Add(26925, 27525);
            ret.Add(24525, 27525);
            ret.Add(105525, 27525);
            ret.Add(46005, 27525);
            ret.Add(8205, 27525);
            ret.Add(38325, 27525);
            ret.Add(30525, 27525);
            ret.Add(14565, 27525);
            ret.Add(30645, 27525);
            ret.Add(8085, 27525);
            ret.Add(38205, 27525);
            ret.Add(8325, 27525);
            ret.Add(30765, 27525);
            ret.Add(7965, 27525);
            ret.Add(38085, 27525);
            ret.Add(44805, 27525);
            ret.Add(8445, 27525);
            ret.Add(37845, 27525);
            ret.Add(37965, 27525);
            ret.Add(27045, 27525);
            ret.Add(46245, 27525);
            ret.Add(24765, 27525);
            ret.Add(24885, 27525);
            ret.Add(25005, 27525);
            ret.Add(34365, 27525);
            ret.Add(46365, 27525);
            ret.Add(26445, 27525);
            ret.Add(46485, 27525);
            ret.Add(34485, 27525);
            ret.Add(38445, 27525);
            ret.Add(34605, 27525);
            ret.Add(38565, 27525);
            ret.Add(34245, 27525);
            ret.Add(14805, 27525);
            ret.Add(30885, 27525);
            ret.Add(99045, 27525);
            ret.Add(99165, 27525);
            ret.Add(99285, 27525);
            ret.Add(10365, 27525);
            ret.Add(90405, 27525);
            ret.Add(10125, 27525);
            ret.Add(44925, 27525);
            ret.Add(10245, 27525);
            ret.Add(31005, 27525);
            ret.Add(17565, 27525);
            ret.Add(14925, 27525);
            ret.Add(45045, 27525);
            ret.Add(17685, 27525);
            ret.Add(17805, 27525);
            ret.Add(17925, 27525);
            ret.Add(33765, 27525);
            ret.Add(96885, 27525);
            ret.Add(97005, 27525);
            ret.Add(97125, 27525);
            ret.Add(33885, 27525);
            ret.Add(94845, 27525);
            ret.Add(94965, 27525);
            ret.Add(94725, 27525);
            ret.Add(18405, 27525);
            ret.Add(34005, 27525);
            ret.Add(98685, 27525);
            ret.Add(98805, 27525);
            ret.Add(98925, 27525);
            ret.Add(34125, 27525);
            ret.Add(95205, 27525);
            ret.Add(95325, 27525);
            ret.Add(95085, 27525);
            ret.Add(18525, 27525);
            ret.Add(10725, 27525);
            ret.Add(31125, 27525);
            ret.Add(10605, 27525);
            ret.Add(99405, 27525);
            ret.Add(99525, 27525);
            ret.Add(99645, 27525);
            ret.Add(11085, 27525);
            ret.Add(10965, 27525);
            ret.Add(10485, 27525);
            ret.Add(10845, 27525);
            ret.Add(31245, 27525);
            ret.Add(102405, 27525);
            ret.Add(102525, 27525);
            ret.Add(102285, 27525);
            ret.Add(40245, 27525);
            ret.Add(31365, 27525);
            ret.Add(18765, 27525);
            ret.Add(102765, 27525);
            ret.Add(102645, 27525);
            ret.Add(102885, 27525);
            ret.Add(18885, 27525);
            ret.Add(40365, 27525);
            ret.Add(31485, 27525);
            ret.Add(19125, 27525);
            ret.Add(101805, 27525);
            ret.Add(101925, 27525);
            ret.Add(101685, 27525);
            ret.Add(19485, 27525);
            ret.Add(19245, 27525);
            ret.Add(40485, 27525);
            ret.Add(31605, 27525);
            ret.Add(19725, 27525);
            ret.Add(45165, 27525);
            ret.Add(90765, 27525);
            ret.Add(19845, 27525);
            ret.Add(31725, 27525);
            ret.Add(19965, 27525);
            ret.Add(100845, 27525);
            ret.Add(100725, 27525);
            ret.Add(100965, 27525);
            ret.Add(20085, 27525);
            ret.Add(40605, 27525);
            ret.Add(45285, 27525);
            ret.Add(90645, 27525);
            ret.Add(28485, 27525);
            ret.Add(4125, 27525);
            ret.Add(36045, 27525);
            ret.Add(28125, 27525);
            ret.Add(20205, 27525);
            ret.Add(28245, 27525);
            ret.Add(20325, 27525);
            ret.Add(28005, 27525);
            ret.Add(4005, 27525);
            ret.Add(35685, 27525);
            ret.Add(4365, 27525);
            ret.Add(20445, 27525);
            ret.Add(20565, 27525);
            ret.Add(4485, 27525);
            ret.Add(28365, 27525);
            ret.Add(3885, 27525);
            ret.Add(35565, 27525);
            ret.Add(43005, 27525);
            ret.Add(43125, 27525);
            ret.Add(4245, 27525);
            ret.Add(31845, 27525);
            ret.Add(20805, 27525);
            ret.Add(104205, 27525);
            ret.Add(104085, 27525);
            ret.Add(104325, 27525);
            ret.Add(20925, 27525);
            ret.Add(40725, 27525);
            ret.Add(35925, 27525);
            ret.Add(28725, 27525);
            ret.Add(36165, 27525);
            ret.Add(28605, 27525);
            ret.Add(35805, 27525);
            ret.Add(36525, 27525);
            ret.Add(28845, 27525);
            ret.Add(3405, 27525);
            ret.Add(103125, 27525);
            ret.Add(103245, 27525);
            ret.Add(103005, 27525);
            ret.Add(36405, 27525);
            ret.Add(3645, 27525);
            ret.Add(3765, 27525);
            ret.Add(3285, 27525);
            ret.Add(36285, 27525);
            ret.Add(3525, 27525);
            ret.Add(31965, 27525);
            ret.Add(40845, 27525);
            ret.Add(32085, 27525);
            ret.Add(21045, 27525);
            ret.Add(103485, 27525);
            ret.Add(103365, 27525);
            ret.Add(103605, 27525);
            ret.Add(21165, 27525);
            ret.Add(40965, 27525);
            ret.Add(91005, 27525);
            ret.Add(32205, 27525);
            ret.Add(21285, 27525);
            ret.Add(101085, 27525);
            ret.Add(101205, 27525);
            ret.Add(104445, 27525);
            ret.Add(21405, 27525);
            ret.Add(41085, 27525);
            ret.Add(45405, 27525);
            ret.Add(32325, 27525);
            ret.Add(102045, 27525);
            ret.Add(104565, 27525);
            ret.Add(102165, 27525);
            ret.Add(21645, 27525);
            ret.Add(41205, 27525);
            ret.Add(14685, 27525);
            ret.Add(41325, 27525);
            ret.Add(41445, 27525);
            ret.Add(32445, 27525);
            ret.Add(8805, 27525);
            ret.Add(101445, 27525);
            ret.Add(101325, 27525);
            ret.Add(101565, 27525);
            ret.Add(9045, 27525);
            ret.Add(8925, 27525);
            ret.Add(8685, 27525);
            ret.Add(105405, 27525);
            ret.Add(43245, 27525);
            ret.Add(9165, 27525);
            ret.Add(90885, 27525);
            ret.Add(27405, 27525);
            ret.Add(1845, 27525);
            ret.Add(35205, 27525);
            ret.Add(21765, 27525);
            ret.Add(22365, 27525);
            ret.Add(27285, 27525);
            ret.Add(1725, 27525);
            ret.Add(92925, 27525);
            ret.Add(93165, 27525);
            ret.Add(93045, 27525);
            ret.Add(35325, 27525);
            ret.Add(87405, 27525);
            ret.Add(2205, 27525);
            ret.Add(2085, 27525);
            ret.Add(35445, 27525);
            ret.Add(83085, 27525);
            ret.Add(20685, 27525);
            ret.Add(2325, 27525);
            ret.Add(27165, 27525);
            ret.Add(1605, 27525);
            ret.Add(34845, 27525);
            ret.Add(34965, 27525);
            ret.Add(42405, 27525);
            ret.Add(42645, 27525);
            ret.Add(42525, 27525);
            ret.Add(1965, 27525);
            ret.Add(35085, 27525);
            ret.Add(29565, 27525);
            ret.Add(29085, 27525);
            ret.Add(2685, 27525);
            ret.Add(36885, 27525);
            ret.Add(29445, 27525);
            ret.Add(22725, 27525);
            ret.Add(29205, 27525);
            ret.Add(2565, 27525);
            ret.Add(100485, 27525);
            ret.Add(100605, 27525);
            ret.Add(100365, 27525);
            ret.Add(36765, 27525);
            ret.Add(3165, 27525);
            ret.Add(2925, 27525);
            ret.Add(3045, 27525);
            ret.Add(29325, 27525);
            ret.Add(2445, 27525);
            ret.Add(36645, 27525);
            ret.Add(42885, 27525);
            ret.Add(42765, 27525);
            ret.Add(2805, 27525);
            ret.Add(9885, 27525);
            ret.Add(10005, 27525);
            ret.Add(9285, 27525);
            ret.Add(32565, 27525);
            ret.Add(9525, 27525);
            ret.Add(93765, 27525);
            ret.Add(93885, 27525);
            ret.Add(93645, 27525);
            ret.Add(9765, 27525);
            ret.Add(123285, 27525);
            ret.Add(9405, 27525);
            ret.Add(93405, 27525);
            ret.Add(38685, 27525);
            ret.Add(45525, 27525);
            ret.Add(9645, 27525);
            ret.Add(77205, 27525);
            ret.Add(41685, 27525);
            ret.Add(885, 27525);
            ret.Add(82965, 27525);
            ret.Add(49485, 27525);
            ret.Add(49605, 27525);
            ret.Add(55485, 27525);
            ret.Add(50085, 27525);
            ret.Add(54645, 27525);
            ret.Add(49845, 27525);
            ret.Add(49725, 27525);
            ret.Add(49965, 27525);
            ret.Add(50325, 27525);
            ret.Add(50445, 27525);
            ret.Add(52725, 27525);
            ret.Add(50565, 27525);
            ret.Add(50685, 27525);
            ret.Add(123765, 27525);
            ret.Add(54765, 27525);
            ret.Add(123525, 27525);
            ret.Add(74205, 27525);
            ret.Add(74325, 27525);
            ret.Add(123645, 27525);
            ret.Add(56205, 27525);
            ret.Add(51045, 27525);
            ret.Add(54405, 27525);
            ret.Add(50925, 27525);
            ret.Add(54525, 27525);
            ret.Add(51165, 27525);
            ret.Add(55965, 27525);
            ret.Add(55845, 27525);
            ret.Add(52605, 27525);
            ret.Add(51405, 27525);
            ret.Add(51525, 27525);
            ret.Add(55245, 27525);
            ret.Add(51645, 27525);
            ret.Add(55605, 27525);
            ret.Add(55725, 27525);
            ret.Add(88245, 27525);
            ret.Add(54045, 27525);
            ret.Add(55365, 27525);
            ret.Add(54285, 27525);
            ret.Add(74685, 27525);
            ret.Add(54165, 27525);
            ret.Add(54885, 27525);
            ret.Add(52485, 27525);
            ret.Add(51765, 27525);
            ret.Add(56085, 27525);
            ret.Add(52365, 27525);
            ret.Add(51885, 27525);
            ret.Add(55005, 27525);
            ret.Add(52005, 27525);
            ret.Add(52125, 27525);
            ret.Add(55125, 27525);
            ret.Add(52245, 27525);
            ret.Add(1125, 27525);
            ret.Add(91965, 27525);
            ret.Add(92085, 27525);
            ret.Add(1005, 27525);
            ret.Add(82845, 27525);
            ret.Add(92565, 27525);
            ret.Add(82725, 27525);
            ret.Add(37245, 27525);
            ret.Add(48285, 27525);
            ret.Add(46845, 27525);
            ret.Add(74445, 27525);
            ret.Add(48885, 27525);
            ret.Add(38925, 27525);
            ret.Add(47085, 27525);
            ret.Add(41565, 27525);
            ret.Add(48765, 27525);
            ret.Add(46725, 27525);
            ret.Add(42165, 27525);
            ret.Add(42285, 27525);
            ret.Add(41925, 27525);
            ret.Add(49005, 27525);
            ret.Add(42045, 27525);
            ret.Add(48405, 27525);
            ret.Add(48165, 27525);
            ret.Add(88125, 27525);
            ret.Add(48645, 27525);
            ret.Add(49245, 27525);
            ret.Add(49365, 27525);
            ret.Add(91365, 27525);
            ret.Add(49125, 27525);
            ret.Add(56325, 27525);
            ret.Add(70605, 27525);
            ret.Add(56445, 27525);
            ret.Add(56565, 27525);
            ret.Add(48525, 27525);
            ret.Add(64245, 27525);
            ret.Add(64965, 27525);
            ret.Add(66765, 27525);
            ret.Add(70725, 27525);
            ret.Add(74565, 27525);
            ret.Add(64845, 27525);
            ret.Add(62685, 27525);
            ret.Add(65085, 27525);
            ret.Add(70845, 27525);
            ret.Add(65205, 27525);
            ret.Add(62805, 27525);
            ret.Add(70965, 27525);
            ret.Add(65565, 27525);
            ret.Add(91125, 27525);
            ret.Add(65685, 27525);
            ret.Add(71085, 27525);
            ret.Add(62925, 27525);
            ret.Add(64725, 27525);
            ret.Add(65445, 27525);
            ret.Add(65325, 27525);
            ret.Add(73005, 27525);
            ret.Add(63045, 27525);
            ret.Add(64605, 27525);
            ret.Add(64485, 27525);
            ret.Add(64365, 27525);
            ret.Add(65925, 27525);
            ret.Add(71205, 27525);
            ret.Add(73845, 27525);
            ret.Add(73965, 27525);
            ret.Add(65805, 27525);
            ret.Add(63285, 27525);
            ret.Add(63405, 27525);
            ret.Add(66045, 27525);
            ret.Add(71325, 27525);
            ret.Add(66165, 27525);
            ret.Add(81405, 27525);
            ret.Add(63165, 27525);
            ret.Add(74805, 27525);
            ret.Add(72765, 27525);
            ret.Add(123885, 27525);
            ret.Add(72885, 27525);
            ret.Add(61485, 27525);
            ret.Add(63885, 27525);
            ret.Add(66885, 27525);
            ret.Add(71445, 27525);
            ret.Add(77325, 27525);
            ret.Add(63525, 27525);
            ret.Add(66405, 27525);
            ret.Add(71565, 27525);
            ret.Add(66285, 27525);
            ret.Add(64125, 27525);
            ret.Add(74925, 27525);
            ret.Add(64005, 27525);
            ret.Add(63645, 27525);
            ret.Add(66525, 27525);
            ret.Add(71685, 27525);
            ret.Add(66645, 27525);
            ret.Add(63765, 27525);
            ret.Add(62445, 27525);
            ret.Add(71805, 27525);
            ret.Add(56805, 27525);
            ret.Add(72045, 27525);
            ret.Add(56685, 27525);
            ret.Add(124365, 27525);
            ret.Add(58485, 27525);
            ret.Add(56925, 27525);
            ret.Add(60165, 27525);
            ret.Add(60285, 27525);
            ret.Add(58605, 27525);
            ret.Add(57045, 27525);
            ret.Add(60405, 27525);
            ret.Add(59805, 27525);
            ret.Add(60525, 27525);
            ret.Add(59565, 27525);
            ret.Add(72165, 27525);
            ret.Add(60765, 27525);
            ret.Add(60645, 27525);
            ret.Add(59445, 27525);
            ret.Add(72285, 27525);
            ret.Add(60885, 27525);
            ret.Add(61005, 27525);
            ret.Add(59685, 27525);
            ret.Add(57165, 27525);
            ret.Add(61245, 27525);
            ret.Add(87525, 27525);
            ret.Add(59325, 27525);
            ret.Add(57405, 27525);
            ret.Add(57285, 27525);
            ret.Add(72405, 27525);
            ret.Add(61605, 27525);
            ret.Add(59925, 27525);
            ret.Add(61725, 27525);
            ret.Add(59205, 27525);
            ret.Add(57525, 27525);
            ret.Add(57765, 27525);
            ret.Add(57885, 27525);
            ret.Add(58005, 27525);
            ret.Add(57645, 27525);
            ret.Add(62565, 27525);
            ret.Add(60045, 27525);
            ret.Add(124485, 27525);
            ret.Add(61365, 27525);
            ret.Add(59085, 27525);
            ret.Add(58125, 27525);
            ret.Add(72525, 27525);
            ret.Add(61965, 27525);
            ret.Add(61845, 27525);
            ret.Add(72645, 27525);
            ret.Add(58965, 27525);
            ret.Add(58845, 27525);
            ret.Add(58245, 27525);
            ret.Add(62085, 27525);
            ret.Add(62205, 27525);
            ret.Add(58725, 27525);
            ret.Add(58365, 27525);
            ret.Add(62325, 27525);
            ret.Add(85365, 27525);
            ret.Add(105045, 27525);
            ret.Add(84405, 27525);
            ret.Add(105165, 27525);
            ret.Add(87165, 27525);
            ret.Add(84165, 27525);
            ret.Add(88725, 27525);
            ret.Add(89085, 27525);
            ret.Add(108165, 27525);
            ret.Add(122685, 27525);
            ret.Add(106125, 27525);
            ret.Add(81645, 27525);
            ret.Add(89205, 27525);
            ret.Add(89325, 27525);
            ret.Add(108525, 27525);
            ret.Add(84525, 27525);
            ret.Add(84645, 27525);
            ret.Add(121005, 27525);
            ret.Add(109005, 27525);
            ret.Add(105765, 27525);
            ret.Add(109125, 27525);
            ret.Add(124845, 27525);
            ret.Add(123165, 27525);
            ret.Add(119085, 27525);
            ret.Add(84285, 27525);
            ret.Add(105885, 27525);
            ret.Add(121125, 27525);
            ret.Add(106605, 27525);
            ret.Add(106485, 27525);
            ret.Add(107205, 27525);
            ret.Add(122085, 27525);
            ret.Add(108645, 27525);
            ret.Add(109245, 27525);
            ret.Add(92325, 27525);
            ret.Add(109365, 27525);
            ret.Add(107325, 27525);
            ret.Add(107685, 27525);
            ret.Add(120885, 27525);
            ret.Add(107445, 27525);
            ret.Add(121245, 27525);
            ret.Add(88485, 27525);
            ret.Add(88365, 27525);
            ret.Add(107925, 27525);
            ret.Add(108045, 27525);
            ret.Add(122565, 27525);
            ret.Add(106005, 27525);
            ret.Add(76485, 27525);
            ret.Add(89805, 27525);
            ret.Add(121605, 27525);
            ret.Add(121845, 27525);
            ret.Add(121965, 27525);
            ret.Add(124725, 27525);
            ret.Add(107565, 27525);
            ret.Add(109485, 27525);
            ret.Add(124605, 27525);
            ret.Add(120525, 27525);
            ret.Add(88965, 27525);
            ret.Add(108405, 27525);
            ret.Add(88845, 27525);
            ret.Add(108885, 27525);
            ret.Add(81765, 27525);
            ret.Add(77685, 27645);
            ret.Add(90525, 27645);
            ret.Add(79605, 27645);
            ret.Add(85605, 27645);
            ret.Add(80685, 27645);
            ret.Add(81045, 27645);
            ret.Add(105645, 27645);
            ret.Add(78885, 27645);
            ret.Add(80565, 27645);
            ret.Add(122925, 27645);
            ret.Add(85725, 27645);
            ret.Add(81165, 27645);
            ret.Add(79965, 27645);
            ret.Add(80085, 27645);
            ret.Add(80205, 27645);
            ret.Add(93525, 27645);
            ret.Add(80445, 27645);
            ret.Add(123045, 27645);
            ret.Add(52965, 27885);
            ret.Add(53205, 27885);
            ret.Add(67005, 27885);
            ret.Add(86565, 27885);
            ret.Add(86685, 27885);
            ret.Add(53805, 28005);
            ret.Add(53925, 28005);
            ret.Add(53685, 28005);
            ret.Add(53445, 28005);
            ret.Add(53565, 28005);
            ret.Add(89565, 28005);
            ret.Add(47445, 28005);
            ret.Add(47205, 28005);
            ret.Add(47325, 28005);
            ret.Add(47685, 28005);
            ret.Add(47805, 28005);
            ret.Add(47925, 28005);
            ret.Add(69165, 28005);
            ret.Add(68805, 28005);
            ret.Add(68925, 28005);
            ret.Add(69045, 28005);
            ret.Add(69405, 28005);
            ret.Add(69285, 28005);
            ret.Add(89685, 28005);
            ret.Add(69645, 28005);
            ret.Add(67845, 28005);
            ret.Add(53325, 28005);
            ret.Add(67605, 28005);
            ret.Add(67725, 28005);
            ret.Add(67965, 28005);
            ret.Add(68685, 28005);
            ret.Add(68445, 28005);
            ret.Add(68085, 28005);
            ret.Add(68205, 28005);
            ret.Add(68325, 28005);
            ret.Add(68565, 28005);
            ret.Add(69525, 28005);
            ret.Add(69765, 28005);
            ret.Add(70005, 28005);
            ret.Add(69885, 28005);
            ret.Add(70125, 28005);
            ret.Add(70365, 28005);
            ret.Add(70485, 28005);
            ret.Add(89445, 28005);
            ret.Add(91725, 28005);
            ret.Add(91605, 28005);
            ret.Add(106365, 28005);
            ret.Add(91485, 28005);
            ret.Add(106245, 28005);
            ret.Add(104925, 28005);
            ret.Add(90285, 27765);
            ret.Add(90045, 27765);
            ret.Add(92205, 27765);
            ret.Add(48045, 27765);
            ret.Add(76965, 27765);
            ret.Add(87765, 27765);
            ret.Add(67365, 27765);
            ret.Add(67245, 27765);
            ret.Add(67125, 27765);
            ret.Add(67485, 27765);
            ret.Add(86805, 27765);
            ret.Add(73365, 27765);
            ret.Add(84765, 27765);
            ret.Add(100245, 27765);
            ret.Add(121485, 27765);
            ret.Add(104805, 27765);
            return ret;
        }
    }
}
