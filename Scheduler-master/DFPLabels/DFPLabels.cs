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

namespace DFPLabels
{
    public class DFPLabels : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            process.log("Getting all Labels");

            // Get the LabelService.
            LabelService labelService = (LabelService)interfaceUser.GetService(DfpService.v201508.LabelService);
            labelService.Timeout = 300000; // 5 min

            // Create a statement to get all labels.
            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id DESC")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            // Set default for page.
            LabelPage page = new LabelPage();

           do {
              // Get labels by statement.
              process.log("Loading...");
              page = labelService.getLabelsByStatement(statementBuilder.ToStatement()); 
              process.log(" .. Loaded ");

              if (page.results != null) {
                int i = page.startIndex;
                foreach (Label label in page.results) {
                    process.log(++i + "/" + page.totalResultSetSize + " " + label.id + " : " + label.name);
                    DFPBase.checkDfpObject(process, "dfp_labels", "labelId", label);
                }
              }
              statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

           process.log("Number of items found: " + page.totalResultSetSize);
        }
    }
}
