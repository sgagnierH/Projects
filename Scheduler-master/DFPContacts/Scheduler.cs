using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Reflection;
using System.Collections;

namespace Tc.TcMedia.Scheduler
{
    public class Controller
    {
        static Db db = new Db();

        static void Main(string[] args)
        {
            Schedule theSchedule = null;
            Guid guid = Guid.NewGuid();

            while (1 == 1)
            {
                theSchedule = db.getNextSchedule(guid);
                if (theSchedule == null)
                {
                    Console.WriteLine("Zzzz");
                    System.Threading.Thread.Sleep(1000);
                } 
                else 
                {
                    try
                    {
                        Console.WriteLine(System.DateTime.Now.ToString() + " " + theSchedule.Name);
                        string path = Assembly.GetExecutingAssembly().Location.Replace("Scheduler.exe", "");
                        string dllPath = path + theSchedule.Name + ".dll";
                        if (!File.Exists(dllPath))
                            dllPath = path.Replace("Scheduler", theSchedule.Name) + theSchedule.Name + ".dll";

                        Assembly assembly = Assembly.LoadFile(dllPath);
                        Type theType = assembly.GetType(theSchedule.Name + "." + theSchedule.Name);
                        object c = Activator.CreateInstance(theType, null);
                        theType.InvokeMember("Run", BindingFlags.InvokeMethod, null, c, new object[] {theSchedule});

                        db.setSuccess(theSchedule);
                        Console.WriteLine("Done");
                    }
                    catch(Exception ex)
                    {
                        db.setFailure(theSchedule, (ex.Message.Length < 200) ? ex.Message : ex.Message.Substring(0,199));
                    }
                }
            }
        }
    }
}
