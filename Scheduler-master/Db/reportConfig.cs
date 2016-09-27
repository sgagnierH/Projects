using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;
using Google.Api.Ads.Common.Util;

namespace Tc.TcMedia.Scheduler
{
    public class ReportConfig
    {
        public Process process;
        public ReportJob reportJob { get; set; }
        public string filename { get; set; }
        public ExportFormat format { get; set; }
        public string dateRange { get; set; }
        public string statement { get; set; }
        public bool reportDownloaded { get; set; }
        public System.DateTime endDate { get; set; }
    }
}
