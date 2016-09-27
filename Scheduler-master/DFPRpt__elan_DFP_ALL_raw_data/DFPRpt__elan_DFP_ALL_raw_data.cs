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

namespace DFPRpt__elan_DFP_ALL_raw_data
{
    public class DFPRpt__elan_DFP_ALL_raw_data : DFPRpt_base, iDFPReportDownloader
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
                Dimension.ORDER_NAME,
                Dimension.ADVERTISER_NAME,
                Dimension.CREATIVE_SIZE,
                Dimension.LINE_ITEM_ID,
                Dimension.AD_UNIT_ID,
                Dimension.ORDER_ID,
                Dimension.ADVERTISER_ID,
            };

            reportJob.reportQuery.dimensionAttributes = new DimensionAttribute[] {
                DimensionAttribute.LINE_ITEM_START_DATE_TIME,
                DimensionAttribute.LINE_ITEM_END_DATE_TIME,
                DimensionAttribute.ORDER_PO_NUMBER,
                DimensionAttribute.ADVERTISER_LABELS,
                DimensionAttribute.ORDER_SALESPERSON,
                DimensionAttribute.LINE_ITEM_COST_TYPE,
                DimensionAttribute.LINE_ITEM_COST_PER_UNIT,
                DimensionAttribute.LINE_ITEM_CONTRACTED_QUANTITY,
            };

            reportJob.reportQuery.columns = new Column[] {
                Column.TOTAL_LINE_ITEM_LEVEL_IMPRESSIONS,
                Column.TOTAL_LINE_ITEM_LEVEL_CLICKS,
                Column.AD_SERVER_IMPRESSIONS,
                Column.AD_SERVER_CLICKS,
                Column.AD_SERVER_CPM_AND_CPC_REVENUE,
            };

            reportJob.reportQuery.statement = new Statement();
            
            config.reportJob = reportJob;
        }
        public void postProcess(ReportConfig config, string timespan)
        {
            fixFile(ref config, true, true);
            appendFile(config.filename, "Total\n");
        }
    }
}
