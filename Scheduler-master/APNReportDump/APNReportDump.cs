using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tc.TcMedia.Apn;
using Tc.TcMedia.Scheduler;
using Newtonsoft.Json;

namespace APNReportDump
{
    public class APNReportDump : iScheduler
    {
        public void Run(Process process)
        {
            SeatConfig config = APNBase.getSeats(process);
            Command cmd = JsonConvert.DeserializeObject<Command>(process.schedule.Config);

            foreach (Seat seat in config.seats)
            {
                if (seat.no != cmd.seat && cmd.seat != -1)
                    break;

                System.DateTime end = System.DateTime.UtcNow.AddHours(-2); // Reporting data is about 2 hours old.

                seat.auth = new APNAuth(seat.username, seat.password);

                if (!seat.auth.authenticated) throw new Exception("Can't login to account no " + seat.no);
                string data = cmd.apnquery;
                dynamic report = APNBase.callApi(process, seat.auth, "report", data);

                cmd.filename = cmd.filename.Replace("%seat%", seat.no.ToString()).Replace("yyyyMMdd", System.DateTime.Now.ToString("yyyyMMdd"));
                cmd.title = cmd.title.Replace("%seat%", seat.no.ToString());
                cmd.body = cmd.body.Replace("%seat%", seat.no.ToString());

                string filePath = "c:\\temp\\" + cmd.filename;
                if (report == null) throw new Exception("No report from Appnexus");

                string reportId = report.report_id;
                process.log("Report Id for Seat no " + seat.no + ": " + reportId + " ");
                APNBase.waitForReport(process, seat.auth, reportId, filePath);

                Db.sendMail(process, cmd.toEmail, cmd.title, cmd.body, filePath);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }
    }
    public class Command
    {
        public string apnquery { get; set;}
        public int seat { get; set; }
        public string filename { get; set; }
        public string title { get; set; }
        public string body { get; set; }
        public string toEmail { get; set; }
    }
}
