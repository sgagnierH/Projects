using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tc.TcMedia.Scheduler
{
    public class Schedule
    {
        private long scheduleId;
        private string config;
        private string schedule;
        private bool enabled;
        private DateTime nextRun;
        private DateTime lastSuccessfulRun;
        private int repeatEach;
        private string repeatInterval;
        private DateTime lastRun;
        private int errorCount;
        private string errorMessage;
        private bool runAllSteps;

        public long ScheduleId { get { return scheduleId; } set { scheduleId = value; } }
        public string Config { get { return config; } set { config = value; } }
        public string Name { get { return schedule; } set { schedule = value; } }
        public bool Enabled { get { return enabled; } set { enabled = value; } }
        public DateTime NextRun { get { return nextRun; } set { nextRun = value; } }
        public DateTime LastSuccessfulRun { get { return lastSuccessfulRun; } set { lastSuccessfulRun = value; } }
        public int RepeatEach { get { return repeatEach; } set { repeatEach = value; } }
        public string RepeatInterval { get { return repeatInterval; } set { repeatInterval = value; } }
        public DateTime LastRun { get { return lastRun; } set { lastRun = value; } }
        public int ErrorCount { get { return errorCount; } set { errorCount = value; } }
        public string ErrorMessage { get { return errorMessage; } set { errorMessage = value; } }
        public bool RunAllSteps { get { return runAllSteps; } set { runAllSteps = value; } }
        public DateTime CalculatedNextRun
        {
            get
            {
                DateTime retDateTime = nextRun;
                switch (repeatInterval)
                {
                    case "NONE":
                        retDateTime = DateTime.MaxValue;
                        break;
                    case "MINUTE":
                    case "MINUTES":
                        if (runAllSteps)
                            retDateTime = retDateTime.AddMinutes(repeatEach);
                        else
                            while (retDateTime < System.DateTime.Now)
                                retDateTime = retDateTime.AddMinutes(repeatEach);
                        break;
                    case "HOUR":
                    case "HOURS":
                        if(runAllSteps)
                            retDateTime = retDateTime.AddHours(repeatEach); 
                        else
                            while(retDateTime < System.DateTime.Now)
                                retDateTime = retDateTime.AddHours(repeatEach); 
                        break;
                    case "DAY": 
                    case "DAYS":
                        if(runAllSteps)
                            retDateTime = retDateTime.AddDays(repeatEach); 
                        else
                            while (retDateTime < System.DateTime.Now)
                                retDateTime = retDateTime.AddDays(repeatEach); 
                        break;
                    case "WEEK":
                    case "WEEKS":
                        if (runAllSteps)
                            retDateTime = retDateTime.AddDays(repeatEach * 7);
                        else
                            while (retDateTime < System.DateTime.Now)
                                retDateTime = retDateTime.AddDays(repeatEach * 7);
                        break;
                    case "MONTH": 
                    case "MONTHS":
                        if(runAllSteps)
                            retDateTime = retDateTime.AddMonths(repeatEach);
                        else
                            while (retDateTime < System.DateTime.Now)
                                retDateTime = retDateTime.AddMonths(repeatEach);
                        break;
                }
                return retDateTime;
            }
        }
    }
}
