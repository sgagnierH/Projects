using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime;
using Tc.TcMedia.Scheduler;
using Tc.TcMedia.Apn;

namespace Launcher
{
    class Launcher
    {
        static void Main(string[] args)
        {
            string cmd;
            string json;

            if (args.Length == 1)
            {
                cmd = args[0];
                json = null;
            }
            else if (args.Length == 2)
            {
                cmd = args[0];
                json = args[1];
            }
            else
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.WriteLine("Enter DLL name to continue");
                    cmd = Console.ReadLine();
                    Console.WriteLine("Write the path of a JSON file to continue");
                    json = Console.ReadLine();
                }
                else
                {
                    throw new Exception("Argument missing"); 
                }
            }

            DateTime startDateTime = System.DateTime.MinValue;
            DateTime endDateTime = System.DateTime.MinValue;
            bool Success = false; 

            Process process = new Process();
            process.guid = Guid.NewGuid();
            process.log("Starting " + process.guid.ToString());  

            try
            {
                process.schedule = Db.getSchedule(process, cmd);

                if (process.schedule == null)
                    throw new Exception("No task to run named " + cmd);

                string path = Assembly.GetExecutingAssembly().Location.Replace("Launcher.exe", "");
                string dllPath = path + process.schedule.Name + ".dll";
                if (!File.Exists(dllPath))
                    dllPath = path.Replace("Launcher", process.schedule.Name) + process.schedule.Name + ".dll";

                Assembly assembly = Assembly.LoadFile(dllPath);
                Type theType = assembly.GetType(process.schedule.Name + "." + process.schedule.Name);
                process.setTitle(System.DateTime.Now.ToString("HH:mm:ss ") + process.schedule.Name);
                iScheduler c = (iScheduler)Activator.CreateInstance(theType, null);
                process.setTitle(process.schedule.Name);

                if (json.Length > 0)
                    process.schedule.Config = File.ReadAllText(json);

                startDateTime = System.DateTime.Now;
                c.Run(process);
                endDateTime = System.DateTime.Now;

                Success = true;
                process.log(process.schedule.Name + " completed");
                Db.setSuccess(process);
            }
            catch (APNApiException ex)
            {
                endDateTime = System.DateTime.Now;
                Db.setFailure(process, (ex.Message.Length < 200) ? ex.Message : ex.Message.Substring(0, 199));
                throw ex;
            }
            catch (Exception ex)
            {
                endDateTime = System.DateTime.Now;
                Db.setFailure(process, (ex.Message.Length < 200) ? ex.Message : ex.Message.Substring(0, 199));

                StringBuilder body = new StringBuilder();
                Exception e = ex;
                body.AppendLine("Schedule : " + process.schedule.Name);
                body.AppendLine("DateTime : " + process.schedule.NextRun);
                string inner = "->";
                while (e != null)
                {
                    body.AppendLine(inner + ex.Message);
                    inner = "-" + inner;
                    e = e.InnerException;
                }
                body.AppendLine("Source : " + ex.Source);
                body.AppendLine("Stack Trace : " + ex.StackTrace);
                Db.sendError(process, body.ToString());
                throw ex;
            }
            finally
            {
                if (process.schedule != null)
                    Db.execSqlCommand(process, "INSERT INTO 0_execution_log (scheduleId, startDateTime, endDateTime, inDaySchedule, success) VALUES (" + process.schedule.ScheduleId + ",'" + Db.toMySqlDate(startDateTime) + "','" + Db.toMySqlDate(endDateTime) + "'," + ((process.inDaySchedule) ? "1" : "0") + "," + ((Success) ? "1" : "0") + ")");
                process.conn.Close();
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.WriteLine("Hit Enter to exit");
                    Console.ReadLine();
                    process.log("Press any key to exit");
                }
            }
        }
    }
}
