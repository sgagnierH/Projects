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

namespace DFPCompanies
{
    public class DFPCompanies : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            string lastModifiedDateTime = "";
            lastModifiedDateTime = Db.getLastModifiedDateTime(process, "dfp_companies");
            process.log("Getting all Companies" + ((lastModifiedDateTime == "") ? "" : " since " + lastModifiedDateTime));

            // Get the CompanyService.
            CompanyService companyService = (CompanyService)interfaceUser.GetService(DfpService.v201508.CompanyService);
            companyService.Timeout = 300000; // 5 min

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("lastModifiedDateTime ASC")
                .Where((lastModifiedDateTime == "") ? "" : "lastModifiedDateTime >= :lastModifiedDateTime")
                .AddValue("lastModifiedDateTime", lastModifiedDateTime)
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            CompanyPage page = new CompanyPage();

            do
            {
                // Get companies by Statement.
                process.log("Loading...");
                page = companyService.getCompaniesByStatement(statementBuilder.ToStatement());
                process.log("Loaded ");

                if (page.results != null && page.results.Length > 0)
                {
                    int i = page.startIndex;
                    foreach (Company company in page.results)
                    {
                        process.log(++i + "/" + page.totalResultSetSize + " " + company.id + " : " + company.name);
                        DFPBase.checkDfpObject(process, "dfp_companies", "companyId", company);
                    }
                }
                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            process.log("Number of items found: " + page.totalResultSetSize);
        }
    }
}
