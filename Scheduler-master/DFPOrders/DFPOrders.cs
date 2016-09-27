using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;
using Google.Api.Ads.Common.Util;
using Tc.TcMedia.Scheduler;
using Tc.TcMedia.Dfp;
using MySql.Data.MySqlClient;

namespace DFPOrders
{
    public class DFPOrders : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            string lastModifiedDateTime = "";
            lastModifiedDateTime = Db.getLastModifiedDateTime(process, "dfp_orders");
            process.log("Getting all Orders" + ((lastModifiedDateTime == "") ? "" : " since " + lastModifiedDateTime));

            OrderService orderService = (OrderService) interfaceUser.GetService(DfpService.v201508.OrderService);
            orderService.Timeout = Db.getTimeout();

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("lastModifiedDateTime ASC")
                .Where((lastModifiedDateTime == "") ? "" : "lastModifiedDateTime >= :lastModifiedDateTime")
                .AddValue("lastModifiedDateTime", lastModifiedDateTime)
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            // Set default for page.
            OrderPage page = new OrderPage();

            int i = 0;
            do {
                // Get orders by Statement.
                int success = 0;
                int retries = 0;
                while(success == 0 && retries < 5)
                {
                    try
                    {
                        process.log("Loading...");
                        page = orderService.getOrdersByStatement(statementBuilder.ToStatement());
                        process.log(" .. Loaded ");
                        success = 1;
                    }
                    catch(Exception ex)
                    {
                        retries++;
                        if (retries == 5)
                        {
                            Console.Write(retries);
                            Db.sendError(process, ex.Message);
                        }
                    }
                }

                if (page.results != null && page.results.Length > 0)
                {
                    foreach (Order order in page.results)
                    {
                        process.log(++i + "/" + page.totalResultSetSize + " : " + order.id + " - " + order.name);
                        DFPBase.checkDfpObject(process, "dfp_orders", "orderId", order);
                    }
                }
                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            process.log("Number of items found: " + page.totalResultSetSize);
        }
    }
}
