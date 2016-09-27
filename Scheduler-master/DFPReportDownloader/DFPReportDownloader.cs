using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tc.TcMedia.Scheduler;
using Tc.TcMedia.Dfp;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;
using Google.Api.Ads.Common.Util;
using Google.Api.Ads.Common.Util.Reports;

namespace DFPReportDownloader
{
    public class DFPReportDownloader : iScheduler
    {
        public void Run(Process process)
        {
            Command cmd = JsonConvert.DeserializeObject<Command>(process.schedule.Config);
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();
            ReportService reportService = (ReportService)interfaceUser.GetService(DfpService.v201508.ReportService);

            // Create report job.
            ReportConfig config = new ReportConfig();
            config.process = process;
            config.reportDownloaded = false;

            try
            {
                iDFPReportDownloader r = getBinary(cmd, process);

                r.define(config, cmd.timespan);

                config.filename = "C:\\Temp\\" + cmd.report + cmd.appendname + "_(" + config.dateRange.Replace(" ", "") + ")_" + System.DateTime.Now.ToString("yyyyMMdd_hhmmss") + ".csv";
                if (cmd.timespan == "LastMonth")
                    config.filename = config.filename.Replace("_elan_", "elan_");

                config.statement = config.reportJob.reportQuery.statement.query;
                if (config.statement == "NORUN")
                {
                    File.WriteAllText(config.filename, "");
                }
                else
                {
                    // Run report.
                    config.reportJob.reportQuery.timeZone = "UTC";
                    config.reportJob = reportService.runReportJob(config.reportJob);
                    process.log("Report job with id = " + config.reportJob.id + " requested. " + cmd.report + " / " + cmd.timespan);

                    ReportUtilities reportUtilities = new ReportUtilities(reportService, config.reportJob.id);

                    // Set download options.
                    ReportDownloadOptions options = new ReportDownloadOptions();
                    options.exportFormat = config.format;
                    if (config.format == ExportFormat.CSV_DUMP)
                        options.useGzipCompression = false;
                    else
                        options.useGzipCompression = true;

                    reportUtilities.reportDownloadOptions = options;

                    // Download the report.
                    using (ReportResponse reportResponse = reportUtilities.GetResponse())
                    {
                        reportResponse.Save(config.filename);
                    }
                }
                r.postProcess(config, cmd.timespan);
                Db.sendMail(process, cmd.toEmail, cmd.report, cmd.body, config.filename);

                if (File.Exists(config.filename))
                    File.Delete(config.filename);
            }
            catch (Exception ex)
            {
                process.log("Unable to process report " + ex.Message);
                throw new Exception("Unable to process report", ex);
            }
        }
        public iDFPReportDownloader getBinary(Command cmd, Process process)
        {
            // Call function named get and the name of the report
            string path = Assembly.GetExecutingAssembly().Location;
            path = path.Substring(0,path.LastIndexOf("\\") + 1);

            string dllPath = path + "DFPRpt_" + cmd.report + ".dll";
            if (!File.Exists(dllPath))
                dllPath = path.Replace(process.schedule.Name, "DFPRpt_" + cmd.report).Replace("VControl", "DFPRpt_" + cmd.report) + "DFPRpt_" + cmd.report + ".dll";

            Assembly assembly = Assembly.LoadFile(dllPath);
            Type theType = assembly.GetType("DFPRpt_" + cmd.report + "." + "DFPRpt_" + cmd.report);
            iDFPReportDownloader r = (iDFPReportDownloader)Activator.CreateInstance(theType, null);
            return r;
        }
    }

    #region base class
    public partial class DFPRpt_base
    {
        public static string DATEFMT = "MMM d, yyyy";

        public void setLastMonth(ref ReportConfig config, ref ReportJob reportJob)
        {
            // Last month = from last month's 1st to last month's last day
            reportJob.reportQuery.dateRangeType = DateRangeType.CUSTOM_DATE;
            System.DateTime endDate = System.DateTime.Now.Date;
            endDate = endDate.AddDays(1 - endDate.Day);
            System.DateTime startDate = endDate.AddMonths(-1);
            endDate = endDate.AddDays(-1);
            reportJob.reportQuery.startDate = DFPBase.toGoogleDate(startDate);
            reportJob.reportQuery.endDate = DFPBase.toGoogleDate(endDate);
            config.dateRange = startDate.ToString(DATEFMT) + " - " + endDate.ToString(DATEFMT);
            config.endDate = endDate;
        }
        public void setMonthToDate(ref ReportConfig config, ref ReportJob reportJob)
        {
            // Month to date = from this month's 1st to today
            reportJob.reportQuery.dateRangeType = DateRangeType.CUSTOM_DATE;
            System.DateTime endDate = System.DateTime.Now.Date;
            System.DateTime startDate = endDate.AddDays(1 - endDate.Day);
            reportJob.reportQuery.startDate = DFPBase.toGoogleDate(startDate);
            reportJob.reportQuery.endDate = DFPBase.toGoogleDate(endDate);
            config.dateRange = startDate.ToString(DATEFMT) + " - " + endDate.ToString(DATEFMT);
            config.endDate = endDate;
        }
        public void setLifetime(ref ReportConfig config, ref ReportJob reportJob)
        {
            // Month to date = from this month's 1st to today
            reportJob.reportQuery.dateRangeType = DateRangeType.CUSTOM_DATE;
            System.DateTime endDate = System.DateTime.Now.Date.AddYears(1);
            System.DateTime startDate = endDate.AddYears(-5);
            reportJob.reportQuery.startDate = DFPBase.toGoogleDate(startDate);
            reportJob.reportQuery.endDate = DFPBase.toGoogleDate(endDate);
            config.dateRange = startDate.ToString(DATEFMT) + " - " + endDate.ToString(DATEFMT);
            config.endDate = endDate;
        }
        public string getRunning2000x2000creativeIds(ReportConfig config)
        {
            // Getting running 2000x2000 creativeIds
            MySqlDataReader dr = Db.getMySqlReader(config.process, "select distinct creativeid from dfp_report_historical_data_v_creative c where c.ym = " + config.endDate.ToString("yyyyMM") + " and c.creativesize='2000x2000'");
            string creativeIds = "";
            while (dr.Read())
                creativeIds += ((creativeIds == "") ? "" : ",") + dr.GetInt64("creativeId").ToString();
            dr.Close();

            if (creativeIds == "") creativeIds = "0";
            return creativeIds;
        }
        public string getRemnantAdvertiserIds(ReportConfig config)
        {
            // Getting Remnant advertiserIds 
            MySqlDataReader dr = Db.getMySqlReader(config.process, "SELECT companyId FROM dfp_companies WHERE name LIKE '%remnant%'");
            string advertiserIds = "";
            while (dr.Read())
                advertiserIds += ((advertiserIds == "") ? "" : ",") + dr.GetInt64("companyId").ToString();
            dr.Close();
            return advertiserIds;
        }
        public string get_fina_UserIds(ReportConfig config)
        {
            // Getting Remnant advertiserIds 
            MySqlDataReader dr = Db.getMySqlReader(config.process, "SELECT userId FROM dfp_users WHERE name LIKE '_fina_%'");
            string userIds = "";
            while (dr.Read())
                userIds += ((userIds == "") ? "" : ",") + dr.GetInt64("userId").ToString();
            dr.Close();
            return userIds;
        }
        public void fixFile(ref ReportConfig config, bool setDateFormat = false, bool replaceHeaderTitles = false)
        {
            // Move to temp file
            if (File.Exists(config.filename + ".tmp"))
                File.Delete(config.filename + ".tmp");
            File.Move(config.filename, config.filename + ".tmp");

            // Read file content
            string content = buildHeader(config) + File.ReadAllText(config.filename + ".tmp", Encoding.UTF8);

            if (setDateFormat)
            {
                // Set date format to Google's
                string pattern = ",\\d{2}(\\d{2})-(\\d{2})-(\\d{2})T\\d{2}:\\d{2}:\\d{2}Z";
                Regex reg = new Regex(pattern);
                content = reg.Replace(content, ",$2/$3/$1");
            }

            if (replaceHeaderTitles)
            {
                // Replace column names (!)
                content = content.Replace("Dimension.LINE_ITEM_NAME", "Line item");
                content = content.Replace("Dimension.AD_UNIT_NAME", "Ad unit");
                content = content.Replace("Dimension.LINE_ITEM_ID", "Line item ID");
                content = content.Replace("Dimension.AD_UNIT_ID", "Ad unit ID");
                content = content.Replace("DimensionAttribute.LINE_ITEM_START_DATE_TIME", "Line item start date");
                content = content.Replace("DimensionAttribute.LINE_ITEM_END_DATE_TIME", "Line item end date");
                content = content.Replace("DimensionAttribute.LINE_ITEM_COST_TYPE", "Cost type");
                content = content.Replace("Column.AD_SERVER_IMPRESSIONS", "Ad server impressions");
                content = content.Replace("Column.AD_SERVER_CLICKS", "Ad server clicks");
            }

            // Save back TokenError original filename
            File.WriteAllText(config.filename, content, Encoding.UTF8);

            // Delete temp file
            File.Delete(config.filename + ".tmp");
        }
        public string buildHeader(ReportConfig config)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\n");
            sb.Append("Report job ID," + config.reportJob.id + "\n");
            sb.Append("Date/Time generated,\"" + System.DateTime.Now.ToUniversalTime().ToString("MMMM dd, yyyy hh:mm:ss tt UTC") + "\"\n");
            sb.Append("Publisher network name,TC- DFP\n");
            sb.Append("User,reporting@rpt.reduxmedia.com\n");
            sb.Append("Report time zone, UTC\n");
            sb.Append("Date range,\"" + config.dateRange + "\"\n");
            sb.Append("PQL query statement,\" " + config.statement + "\"\n");
            sb.Append("\n");
            return sb.ToString();
        }
        public void appendFile(string path, string text)
        {
            StreamWriter sw = new StreamWriter(path, true);
            sw.WriteLine(text);
            sw.Close();
        }
    }
    #endregion

    #region helper classes
    public class Command
    {
        public string report { get; set; }
        public string body { get; set; }
        public string toEmail { get; set; }
        public string timespan { get; set; }
        public string appendname { get; set; }
    }
    #endregion
}
