using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using Tc.TcMedia.Scheduler;
using Newtonsoft.Json;

namespace GENScreenshotSpecifics
{
    public static class MyExtensions
    {
        public static String GetTimestamp(this System.DateTime value)
        {
            return value.ToString("yyyyMMddHHmmssfff");
        }
    }

    public class GENScreenshotSpecifics : iScheduler
    {
        public void Run(Process process)
        {
            Command cmd = JsonConvert.DeserializeObject<Command>(process.schedule.Config);
            ScreenShotDemo.ScreenCapture sc = new ScreenShotDemo.ScreenCapture();
            
            var saveFile = @"C:\DriveTC\Screenshot\" + Db.MakeValidFileName(cmd.campagne) + ".jpeg";
            var proc = System.Diagnostics.Process.Start("chrome.exe", cmd.url);
            // Wait a bit for it to be done. It should be more clean...
            System.Threading.Thread.Sleep(30000);
            // Take a screenshot of the.. screen. (Hopefully we see only chrome)
            sc.CaptureScreenToFile(saveFile, ImageFormat.Jpeg);
            proc.CloseMainWindow();

            Db.sendMail(process, cmd.emails, "New screenshot of: " + cmd.campagne, "Hi,\n\nWe have a new screenshot ready for " + cmd.campagne + "\n\nThanks", saveFile);
        }
    }

    class Command
    {
        public string campagne { get; set; }
        public string url { get; set; }
        public string emails { get; set; }
    }
}
