using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tc.TcMedia.Apn;
using Tc.TcMedia.Scheduler;
using NDesk.Options;
using MySql.Data.MySqlClient;
using System.IO;

namespace APNPerformanceReportLoader
{
    class APNPerformanceReportLoader
    {
        static String input = "";

        static int Main(string[] args)
        {
            var opts = new OptionSet()
            {
                {"i|in=", "the file to read from", o => input = o}
            };

            try
            {
                opts.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine("Please specify options correctly: " + e.Message);
                if (System.Diagnostics.Debugger.IsAttached)
                    Console.ReadKey();
                return -1;
            }
            if (input.Length == 0)
            {
                Console.WriteLine("Please specify an input file, please");
                if (System.Diagnostics.Debugger.IsAttached)
                    Console.ReadKey();
                return -2;
            }
            try
            {
                DoAll();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return -1;
            }
            return 0;
        }
        public static void DoAll()
        {
            var process = new Tc.TcMedia.Scheduler.Process();
            Tc.TcMedia.Apn.SeatConfig config = Tc.TcMedia.Apn.APNBase.getSeats(process);
            var success = false;
            var retry = 0;
            foreach (Seat seat in config.seats)
            {

                while (!success)
                {
                    try
                    {
                        MySqlBulkLoader bl = new MySqlBulkLoader(process.conn);
                        bl.TableName = "_tmp_apn_report_historical_data";
                        bl.Timeout = 600000;
                        bl.FieldTerminator = ",";
                        bl.LineTerminator = "\n";
                        bl.FileName = input;
                        bl.FieldQuotationCharacter = '"';
                        bl.FieldQuotationOptional = true;
                        bl.NumberOfLinesToSkip = 1;
                        Db.execSqlCommand(process, "TRUNCATE " + bl.TableName);

                        var done = false;
                        int retriesLeft = 5;
                        int inserted = 0;
                        while (!done)
                        {
                            try
                            {
                                inserted = bl.Load();
                                done = true;
                            }
                            catch (Exception ex)
                            {
                                retriesLeft--;
                                if (retriesLeft == 0)
                                    throw ex;
                            }
                        }

                        Db.execSqlCommand(process,
                            "INSERT INTO apn_report_historical_data (ym, seat, date, publisherId, placementId, lineitemId, size, impressions, clicks, revenue, cost, profit) SELECT year(date)*100+month(date), " +
                            seat.no +
                            ", date, publisher_id, placement_id, line_item_id, size, impressions, clicks, revenue, cost, profit FROM _tmp_apn_report_historical_data");
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        if (retry == 5)
                            throw new Exception("Error adding file", ex);
                        retry++;
                    }
                    finally
                    {
                        if (File.Exists(input))
                            File.Delete(input);
                    }
                }
            }
        }
    }
}
