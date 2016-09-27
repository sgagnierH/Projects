using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;
using Google.Api.Ads.Common.Util;
using Google.Api.Ads.Common.Util.Reports;
using Tc.TcMedia.Scheduler;
using Tc.TcMedia.Dfp;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

namespace DFPSalesReport
{
    public class DFPSalesReport : Tc.TcMedia.Scheduler.iScheduler
    {
        private Dictionary<string, string> fieldNames = new Dictionary<string,string>();

        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            String fileName = "C:\\Temp\\Report_" + System.DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
            ReportService reportService = (ReportService)interfaceUser.GetService(DfpService.v201508.ReportService);

            // Create report job.
            ReportJob reportJob = new ReportJob();
            reportJob.reportQuery = new ReportQuery();
            reportJob.reportQuery.dimensions = new Dimension[] {
                Dimension.PROPOSAL_ID,
                Dimension.PROPOSAL_NAME,
                Dimension.SALESPERSON_NAME,
                Dimension.ADVERTISER_NAME,
                Dimension.PROPOSAL_AGENCY_NAME,
                Dimension.PROPOSAL_LINE_ITEM_NAME,
                Dimension.LINE_ITEM_ID,
                Dimension.PRODUCT_ID,
                Dimension.PROPOSAL_LINE_ITEM_ID,
            };
            reportJob.reportQuery.dimensionAttributes = new DimensionAttribute[] {
                DimensionAttribute.PROPOSAL_START_DATE_TIME,
                DimensionAttribute.PROPOSAL_END_DATE_TIME,
                DimensionAttribute.PROPOSAL_APPLIED_TEAM_NAMES,
                DimensionAttribute.PROPOSAL_BILLING_SOURCE,
                DimensionAttribute.PROPOSAL_PO_NUMBER,
                DimensionAttribute.PROPOSAL_APPROVAL_STAGE,
                DimensionAttribute.PROPOSAL_STATUS,
                DimensionAttribute.PROPOSAL_IS_SOLD,
                DimensionAttribute.ADVERTISER_EXTERNAL_ID,
                DimensionAttribute.PROPOSAL_AGENCY_COMMENT,
                DimensionAttribute.PROPOSAL_LINE_ITEM_START_DATE_TIME,
                DimensionAttribute.PROPOSAL_LINE_ITEM_END_DATE_TIME,
                DimensionAttribute.PROPOSAL_LINE_ITEM_RATE_TYPE,
                DimensionAttribute.PROPOSAL_LINE_ITEM_TARGET_RATE_NET,
                DimensionAttribute.PROPOSAL_LINE_ITEM_COST_PER_UNIT,
                DimensionAttribute.LINE_ITEM_CONTRACTED_QUANTITY,
                DimensionAttribute.PRODUCT_PRODUCT_TYPE,
                DimensionAttribute.PROPOSAL_LINE_ITEM_ARCHIVAL_STATUS,
            };
            reportJob.reportQuery.columns = new Column[] {
                Column.SALES_TOTAL_TOTAL_BUDGET,
                Column.EXPECTED_REVENUE_EXPECTED_NET_REVENUE,
            };

            reportJob.reportQuery.dateRangeType = DateRangeType.CUSTOM_DATE;
            System.DateTime endDate = System.DateTime.Now.AddYears(5);
            System.DateTime startDate = System.DateTime.Now.AddYears(-10);
            reportJob.reportQuery.startDate = DFPBase.toGoogleDate(startDate);
            reportJob.reportQuery.endDate = DFPBase.toGoogleDate(endDate);

            try
            {
                // Run report.
                reportJob = reportService.runReportJob(reportJob);

                process.log("Getting report " + reportJob.id);
                ReportUtilities reportUtilities = new ReportUtilities(reportService, reportJob.id);

                // Set download options.
                ReportDownloadOptions options = new ReportDownloadOptions();
                options.exportFormat = ExportFormat.CSV_DUMP;
                options.useGzipCompression = false;
                reportUtilities.reportDownloadOptions = options;

                // Download the report.
                using (ReportResponse reportResponse = reportUtilities.GetResponse())
                {
                    reportResponse.Save(fileName);
                }
            }
            catch (Exception ex)
            {
                process.log("Failed to run sales report. Exception says \"" + ex.Message + "\"");
            }

            // clean Table
            process.log("Inserting data in temp table");
            MySqlConnection conn = Db.getConnection();
            try
            {
                MySqlBulkLoader bl = new MySqlBulkLoader(conn);
                bl.TableName = "dfp_report_sales_headers";
                bl.FieldTerminator = ",";
                bl.LineTerminator = "\n";
                bl.FileName = fileName;
                bl.FieldQuotationCharacter = '"';
                bl.FieldQuotationOptional = true;
                bl.NumberOfLinesToSkip = 1;
                Db.execSqlCommand(process, "TRUNCATE " + bl.TableName);
                int inserted = bl.Load();
                process.log(inserted + "lines inserted");
            }
            catch(Exception ex)
            {
                process.log(ex.Message);
                throw new Exception("Error adding file", ex);
            }
            finally
            {
                conn.Close();
                if (File.Exists(fileName))
                    File.Delete(fileName);
            }
        }
    }
}
