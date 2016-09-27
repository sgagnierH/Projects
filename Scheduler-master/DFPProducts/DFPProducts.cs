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

namespace DFPProducts
{
    public class DFPProducts : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            process.log("Getting all Products");

            // Get the Service.
            ProductService service = (ProductService)interfaceUser.GetService(DfpService.v201508.ProductService);
            service.Timeout = 300000; // 5 min

            ProductPage page = new ProductPage();
            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id DESC")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            do
            {
                // Get Proposals by Statement.
                process.log("Loading...");
                page = service.getProductsByStatement(statementBuilder.ToStatement());
                process.log("Loaded ");

                if (page.results != null && page.results.Length > 0)
                {
                    int i = page.startIndex;
                    foreach (Product product in page.results)
                    {
                        process.log(++i + "/" + page.totalResultSetSize + " " + product.id + " : " + product.name);
                        DFPBase.checkDfpObject(process, "dfp_products", "productId", product);
                    }
                }
                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            process.log("Number of items found: " + page.totalResultSetSize);
        }
    }
}
