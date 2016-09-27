using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;
using Google.Api.Ads.Common.Util;
using Tc.TcMedia.Scheduler;
using Tc.TcMedia.Dfp;
using DFPReportDownloader;

namespace DFPRpt__elan_DFP_ALL
{
    public class DFPRpt__elan_DFP_ALL : DFPRpt_base, iDFPReportDownloader
    {
        public void define(ReportConfig config, string timespan)
        {
            config.format = ExportFormat.CSV_DUMP;

            ReportJob reportJob = new ReportJob();
            reportJob.reportQuery = new ReportQuery();
            reportJob.reportQuery.adUnitView = ReportQueryAdUnitView.TOP_LEVEL;

            if (timespan == "MonthToDate")
                setMonthToDate(ref config, ref reportJob);
            else if (timespan == "LastMonth")
                setLastMonth(ref config, ref reportJob);
            else
                throw new Exception("timespan not defined : " + timespan);

            reportJob.reportQuery.dimensions = new Dimension[] {
                Dimension.LINE_ITEM_NAME,
                Dimension.AD_UNIT_NAME,
                Dimension.LINE_ITEM_ID,
                Dimension.AD_UNIT_ID,
            };

            reportJob.reportQuery.dimensionAttributes = new DimensionAttribute[] {
                DimensionAttribute.LINE_ITEM_START_DATE_TIME,
                DimensionAttribute.LINE_ITEM_END_DATE_TIME,
                DimensionAttribute.LINE_ITEM_COST_TYPE,
            };

            reportJob.reportQuery.columns = new Column[] {
                Column.AD_SERVER_IMPRESSIONS,
                Column.AD_SERVER_CLICKS,
            };

            reportJob.reportQuery.statement = new Statement();
            reportJob.reportQuery.statement.query = "where salesperson_id in (" + get_fina_UserIds(config) + ")";

            config.reportJob = reportJob;
        }
        public void postProcess(ReportConfig config, string timespan)
        {
            fixFile(ref config, true, true);
            appendFile(config.filename, "Total\n");
        }
    }
}
