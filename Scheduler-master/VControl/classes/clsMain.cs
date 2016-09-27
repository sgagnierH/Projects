using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Tc.TcMedia.Scheduler.Classes
{
    class clsMain
    {
        [STAThread]
        static void Main()
        {
            StringBuilder errlog = new StringBuilder();
            try
            {
                clsGlobal.g_Processes = new Dictionary<Guid, Process>();
                clsGlobal.g_VControl = new VControl(); 
                Application.Run(clsGlobal.g_VControl);
            }
            catch(Exception ex)
            {
                while(ex != null)
                {
                    errlog.AppendLine("Message : " + ex.Message);
                    errlog.AppendLine("Stack : " + ex.StackTrace);
                    ex = ex.InnerException;
                }

                Console.WriteLine(errlog.ToString());
                Db.sendMail(new Process(), "tcapiaccess@tc.tc", "Error in App", errlog.ToString());
            }

            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
            {
                Exception ex = (Exception)args.ExceptionObject;
                errlog.AppendLine("Unhandled exception: " + ex);
                errlog.AppendLine("Error Message");

                while (ex != null)
                {
                    errlog.AppendLine("Message : " + ex.Message);
                    errlog.AppendLine("Stack : " + ex.StackTrace);
                    ex = ex.InnerException;
                }

                Console.WriteLine(errlog.ToString());
                Db.sendMail(new Process(), "tcapiaccess@tc.tc", "Error in App", errlog.ToString());
                Environment.Exit(1);
            };
        }
    }
}
