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
using System.IO.Compression;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

namespace DFPPerformanceReport
{
    public class DFPPerformanceReport : Tc.TcMedia.Scheduler.iScheduler
    {
        private Dictionary<string, string> fieldNames = new Dictionary<string,string>();

        public void Run(Process process)
        {
            Command cmd = JsonConvert.DeserializeObject<Command>(process.schedule.Config);
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();
            ReportService reportService = (ReportService)interfaceUser.GetService(DfpService.v201508.ReportService);
            string fileName = null;

            // Create report job.
            ReportJob reportJob = new ReportJob();
            reportJob.reportQuery = new ReportQuery();
            reportJob.reportQuery.dimensions = new Dimension[] {
                Dimension.DATE,
                Dimension.AD_UNIT_ID,
                Dimension.ADVERTISER_ID,
                Dimension.ORDER_ID,
                Dimension.LINE_ITEM_ID,
                Dimension.CREATIVE_ID,
            };
            reportJob.reportQuery.columns = new Column[] {
                Column.TOTAL_INVENTORY_LEVEL_IMPRESSIONS,
                Column.TOTAL_INVENTORY_LEVEL_CLICKS,
                Column.AD_SERVER_CPM_AND_CPC_REVENUE,
                Column.AD_SERVER_CPD_REVENUE,
                Column.AD_SERVER_DELIVERY_INDICATOR,
                Column.TOTAL_ACTIVE_VIEW_VIEWABLE_IMPRESSIONS,
                Column.TOTAL_ACTIVE_VIEW_MEASURABLE_IMPRESSIONS_RATE,
                Column.TOTAL_ACTIVE_VIEW_VIEWABLE_IMPRESSIONS_RATE,
                Column.TOTAL_ACTIVE_VIEW_ELIGIBLE_IMPRESSIONS,
                Column.TOTAL_ACTIVE_VIEW_MEASURABLE_IMPRESSIONS,
                Column.AD_SERVER_IMPRESSIONS,
                Column.AD_SERVER_CLICKS,
            };

            reportJob.reportQuery.dateRangeType = DateRangeType.CUSTOM_DATE;
            string sqlClean;
            if (process.schedule.NextRun.Date < System.DateTime.Now.Date)
            {
                reportJob.reportQuery.startDate = DFPBase.toGoogleDate(process.schedule.NextRun.Date);
                reportJob.reportQuery.endDate = DFPBase.toGoogleDate(process.schedule.NextRun.Date);
                sqlClean = " date = '" + Db.toMySqlDate(process.schedule.NextRun.Date) + "'";
            }
            else
            {
                reportJob.reportQuery.startDate = DFPBase.toGoogleDate(System.DateTime.Today.AddDays(-8));
                reportJob.reportQuery.endDate = DFPBase.toGoogleDate(System.DateTime.Today.AddDays(-1));
                sqlClean = " date BETWEEN '" + Db.toMySqlDate(System.DateTime.Today.AddDays(-8)) + "' AND '" + Db.toMySqlDate(System.DateTime.Today.AddDays(-1)) + "'";
            }

            try
            {
                // Run report.
                reportJob = reportService.runReportJob(reportJob);
                fileName = "C:\\Temp\\" + reportJob.id + ".csv";

                process.log("Getting report " + reportJob.id + " range " + sqlClean + " -> " + cmd.tempTable);
                ReportUtilities reportUtilities = new ReportUtilities(reportService, reportJob.id);

                // Set download options.
                ReportDownloadOptions options = new ReportDownloadOptions();
                options.exportFormat = ExportFormat.CSV_DUMP;
                options.useGzipCompression = true;
                reportUtilities.reportDownloadOptions = options;

                // Download the report.
                using (ReportResponse reportResponse = reportUtilities.GetResponse())
                {
                    if (File.Exists(fileName)) File.Delete(fileName);
                    byte[] gzipReport = reportResponse.Download();
                    string reportContents = Encoding.UTF8.GetString(MediaUtilities.DeflateGZipData(gzipReport));

                    using (StreamWriter writer = new StreamWriter(fileName))
                    {
                        string pattern = "\\t\"(\\d+),(\\d?.?\\d)\"\\t";
                        Regex reg = new Regex(pattern);
                        writer.Write(reg.Replace(reportContents, "\t$1$2\t").Replace("CA$", "").Replace("%", ""));
                    }
                }
            }
            catch (Exception ex)
            {
                process.log("Failed to get report. Exception says \"" + ex.Message + "\"");
                throw new Exception("Failed to get report", ex);
            }

            // clean Table
            process.log("Cleaning existing data for " + sqlClean);
            Db.execSqlCommand(process, "DELETE FROM dfp_report_historical_data WHERE " + sqlClean);

            process.log("inserting data in temp table");
            MySqlConnection conn = Db.getConnection();
            try
            {
                MySqlBulkLoader bl = new MySqlBulkLoader(conn);
                bl.TableName = cmd.tempTable;
                bl.FieldTerminator = ",";
                bl.LineTerminator = "\n";
                bl.FileName = fileName;
                bl.FieldQuotationCharacter = '"';
                bl.FieldQuotationOptional = true;
                bl.NumberOfLinesToSkip = 1;
                Db.execSqlCommand(process, "TRUNCATE " + bl.TableName); 
                int inserted = bl.Load();

                process.log("Appending actual data");
                Db.execSqlCommand(process, "INSERT INTO dfp_report_historical_data (ym, date, adUnitId, advertiserId, orderId, lineitemId, creativeId, adUnit, totalImpressions, totalClicks, adServerCPMAndCPCRevenue, adServerCPDRevenue, deliveryIndicator, totalActiveViewViewableImpressions, totalActiveViewPctMeasurableImpressions, totalActiveViewPctViewableImpressions, totalActiveViewEligibleImpressions, totalActiveViewMeasurableImpressions, adServerImpressions, adServerClicks) SELECT year(date)*100+month(date) as ym, date, adUnitId, advertiserId, orderId, lineitemId, creativeId, adUnit, totalImpressions, totalClicks, adServerCPMAndCPCRevenue, adServerCPDRevenue, deliveryIndicator, totalActiveViewViewableImpressions, totalActiveViewPctMeasurableImpressions, totalActiveViewPctViewableImpressions, totalActiveViewEligibleImpressions, totalActiveViewMeasurableImpressions, adServerImpressions, adServerClicks FROM " + bl.TableName);
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

        public void appendReportTable(Process process, MySqlConnection conn, string[] fields, string[] types, string[] data, string tableName)
        {
            StringBuilder param = new StringBuilder(); 
            StringBuilder values = new StringBuilder();

            for (int i = 0; i < fields.Length; i++ )
            {
                if (param.ToString() != "") param.Append(",");
                param.Append(fields[i]);

                if (values.ToString() != "") values.Append(",");
                if (types[i] == "string")
                    values.Append("'" + data[i] + "'");
                else if (data[i] == "N/A")
                    values.Append("null");
                else
                    values.Append(data[i]);
            }

            Db.execSqlCommand(process, conn, "INSERT into " + tableName + " (" + param.ToString() + ") VALUES (" + values.ToString() + ")");
        }
    }

    #region helper classes
    public class Command
    {
        public string tempTable { get; set; }
        Command()
        {
            tempTable = "_tmp_dfp_report_historical_data";
        }
    }
    #endregion
}
