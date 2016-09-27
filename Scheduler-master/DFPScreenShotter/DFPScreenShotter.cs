using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Drawing.Imaging;
using Tc.TcMedia.Scheduler;
using MySql.Data.MySqlClient;

namespace DFPScreenShotter
{
    public class DFPScreenShotter : iScheduler
    {
        public void Run(Process process)
        {
            ScreenShotDemo.ScreenCapture sc = new ScreenShotDemo.ScreenCapture();
            // Get all orders that ends today.
            MySqlDataReader orderReader = null;


            var a = Db.MakeValidFileName("TestTing");

            System.Diagnostics.Process.Start(a);

            var sqlQueryJoin = @"SELECT o.name, o.orderId, c.size_width, c.size_height, c.previewUrl
                                 FROM dfp_orders o
                                 JOIN dfp_lineitems l
                                 ON o.orderId = l.orderId
                                 JOIN dfp_licas licas
                                 ON l.lineItemId = licas.lineitemId
                                 JOIN dfp_creatives c
                                 ON licas.creativeId = c.creativeId
                                 WHERE date(l.endDateTime) = CURDATE() AND ((c.size_width = 300 AND c.size_height = 250) OR (c.size_width = 728 AND c.size_height = 90)) AND o.screenshotpath IS NULL
                                 GROUP BY o.orderId"; // Voire DATE_ADD(CURDATE(), INTERVAL -1 DAY);

            orderReader = Db.getMySqlReader(process, sqlQueryJoin);
            // Get their creatives (preferably the 728x90)
            while (orderReader.Read())
            {
                var urlString = "";
                var orderName = orderReader.GetString(0);
                var orderId = orderReader.GetInt64(1);
                var width = orderReader.GetInt64(2);
                var height = orderReader.GetInt64(3);
                var previewUrl = orderReader.GetString(4);
                var sizes = width + "x" + height;
                var fileName = Db.MakeValidFileName(orderId.ToString());
                var path = @"C:\DriveTC\Screenshot\" + fileName + ".jpg";

                urlString = "tcadsize=" + sizes + "&tcadurl=" + WebUtility.UrlEncode(previewUrl);
                var proc = System.Diagnostics.Process.Start("chrome.exe", "www.lafrontiere.ca?" + urlString);
                // Wait a bit for it to be done.
                System.Threading.Thread.Sleep(2000);
                // Take a screenshot of the.. screen. (Hopefully we see only chrome)
                sc.CaptureScreenToFile(path, ImageFormat.Jpeg);
                proc.Kill();

                Db.execSqlCommand(process, "UPDATE dfp_orders SET screenshotpath='" + path + "' WHERE orderId=" + orderId + ";");
            }
        }
    }
}
