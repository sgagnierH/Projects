using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tc.TcMedia.Apn;
using Tc.TcMedia.Scheduler;
using NDesk.Options;
using System.Diagnostics;

namespace APNPerformanceReportDownloader
{
    class APNPerformanceReportDownloader
    {
        static String start = "";
        static String end = "";
        static int orderid = -1;
	static String outPath = "";
        static String debug = "";
	
        static int Main(string[] args)
        {
            var opts = new OptionSet()
            {
                {"s|start=", "the start date of the report", s => start = s },
                {"e|end=", "the end date of the report", e => end = e },
                {"i|id=", "the specific order needed", o => orderid = int.Parse(o) },
                {"o|out=", "the file to output to", o => outPath = o},
                {"debug", "is debug", d => debug = "yes" }
            };
            if (debug == "yes" && Debugger.IsAttached == false)
                Debugger.Launch();
            try
            {
                opts.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine("Please specify options correctly: " + e.Message);
                if (System.Diagnostics.Debugger.IsAttached)
                    Console.ReadKey();
            }
            if (start.Length == 0 || end.Length == 0)
            {
                Console.WriteLine("Please specify a start and an end date");
                if (System.Diagnostics.Debugger.IsAttached)
                    Console.ReadKey();
                return -2;
            }
            if(outPath.Length == 0)
            {
            	Console.WriteLine("Please specify an output file, please");
            	if (System.Diagnostics.Debugger.IsAttached)
                    Console.ReadKey();
            	return -3;
            }
            try
            {
                DoAll();
            }
            catch(Exception e)
            {
                return -1;
            }
            return 0;
        }

        static void DoAll()
        {
            var process = new Tc.TcMedia.Scheduler.Process();
            Tc.TcMedia.Apn.SeatConfig config = Tc.TcMedia.Apn.APNBase.getSeats(process);

            System.DateTime startDate;
            System.DateTime endDate;
            string sqlClean = "";


            startDate = DateTime.Parse(start);
            endDate = DateTime.Parse(end);
            // sqlClean = " date >= '" + startDate.ToString("yyyy-MM-dd HH:mm:ss") + "' AND date <= '" + endDate.ToString("yyyy-MM-dd HH:mm:ss") + "'";


            string reportEnd = endDate.ToString("yyyy-MM-dd HH:mm:ss");
            string reportStart = startDate.ToString("yyyy-MM-dd HH:mm:ss");

            // clean Table
            // Console.Error.WriteLine("Cleaning existing data for " + sqlClean);
            // Tc.TcMedia.Scheduler.Db.execSqlCommand(process, "DELETE FROM apn_report_historical_data WHERE " + sqlClean);

            foreach (Tc.TcMedia.Apn.Seat seat in config.seats)
            {
                //if (seat.no == 1) break; // only process seat 2
                int retry = 0;
                bool success = false;
                
                seat.auth = new APNAuth(seat.username, seat.password);

                string data = "{\"report\":{\"special_pixel_reporting\":\"false\",\"report_type\":\"network_analytics\",\"timezone\":\"UTC\",\"start_date\":\"" + reportStart + "\",\"end_date\":\"" + reportEnd + "\",\"row_per\":[\"day\",\"publisher_id\",\"placement_id\",\"line_item_id\",\"size\"],\"columns\":[\"day\",\"publisher_id\",\"publisher_name\",\"placement_id\",\"placement_name\",\"line_item_id\",\"line_item_name\",\"size\",\"imps\",\"clicks\",\"revenue\",\"cost\",\"profit\"],\"pivot_report\":false,\"fixed_columns\":[\"day\"],\"show_usd_currency\":\"false\",\"orders\":[\"day\",\"publisher_id\",\"publisher_name\",\"placement_id\",\"placement_name\",\"line_item_id\",\"line_item_name\",\"size\",\"imps\",\"clicks\",\"revenue\",\"cost\",\"profit\"],\"name\":\"rpt\",\"ui_columns\":[\"day\",\"publisher_id\",\"publisher_name\",\"placement_id\",\"placement_name\",\"line_item_id\",\"line_item_name\",\"campaign_id\",\"campaign_name\",\"size\",\"imps\",\"clicks\",\"revenue\",\"cost\",\"profit\"]}}";
                dynamic report = APNBase.callApi(process, seat.auth, "report", data);
                if (report == null) throw new Exception("No report from Appnexus");
                string reportId = report.report_id;

                while (!success)
                {
                    try
                    {
                        APNBase.waitForReport(process, seat.auth, reportId, outPath);
                        success = true;
                    }
                    catch (APNApiException ex)
                    {

                        if (ex.Message.StartsWith("Report Failed"))
                        {
                            throw new APNApiException(ex.Message);
                        }
                        seat.auth = new APNAuth(seat.username, seat.password);

                        retry++;
                        if (retry == 5)
                            throw new APNApiException("Unable to reach AppNexus's API", ex);
                    }
                }
            }
        }
    }
}
