using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tc.TcMedia.Scheduler;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Tc.TcMedia.Apn;
using System.IO;

namespace APNPerformanceReport
{
    public class APNPerformanceReport : iScheduler
    {
        public void Run(Process process)
        {
            SeatConfig config = APNBase.getSeats(process);

            System.DateTime startDate;
            System.DateTime endDate;
            string sqlClean = "";
            string stdout;
            string stderr;

            if (process.schedule.NextRun.Date < System.DateTime.Now.Date)
            {
                startDate = process.schedule.NextRun.Date.AddDays(-1);
                endDate = process.schedule.NextRun.Date;
                sqlClean = " date = '" + startDate.ToString("yyyy-MM-dd HH:mm:ss") + "'";
            }
            else
            {
                startDate = System.DateTime.Today.AddDays(-3);
                endDate = System.DateTime.Today;
                sqlClean = " date >= '" + startDate.ToString("yyyy-MM-dd HH:mm:ss") + "'";
            }

            string reportEnd = endDate.ToString("yyyy-MM-dd HH:mm:ss");
            string reportStart = startDate.ToString("yyyy-MM-dd HH:mm:ss");

            // clean Table
            process.log("Cleaning existing data for " + sqlClean);
            //Db.execSqlCommand(process, "DELETE FROM apn_report_historical_data WHERE " + sqlClean);


            string filePath = "c:/temp/" + endDate.ToString("yyyyMMddhhmmss") + "_2_NetworkAnalytics.csv";
            int err = Db.execCmd("APNPerformanceReportDownloader.exe", "-s \"" + reportStart + "\" -e \"" + reportEnd + "\" -o " + filePath, out stdout, out stderr);
            if(err < 0)
            {
                throw new APNApiException("Error downloading the report");
            }

            process.log("Cleaning existing data for " + sqlClean);
            Db.execSqlCommand(process, "DELETE FROM apn_report_historical_data WHERE " + sqlClean);
            Db.execCmd("APNPerformanceReportLoader.exe", "-i " + filePath, out stdout, out stderr);
        }
    }
    public class placementreport
    {
        public long placementId { get; set; }
        public float revenue { get; set; }
        public long impresssions { get; set; }
    }
    public class Command
    {
        public int updateDfp { get; set; }
    }
}
