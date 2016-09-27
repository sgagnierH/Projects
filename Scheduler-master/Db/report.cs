using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tc.TcMedia.Scheduler
{
    public class Report
    {
        public long reportQueueId;
        public string reportTemplateName;
        public string orderNo;
        public string sfo_orderId;
        public string lang;
        public string emails;
        public long orderId;
        public string source;
        public ReportOptions options;
        public string name;
        public string body;
        public string title;
        public DateTime startDateTime;
        public DateTime endDateTime;
        public bool unlimitedEndDateTime;
        public string poNumber;
        public long salespersonId;
        public string status;
        public long totalBudget;
        public long totalImpressionsDelivered;
        public long totalClicksDelivered;
        public string salespersonName;
        public string salespersonEmail;
        public string advertiser;
    }
    public class ReportOptions
    {
        public string source;
        public string viewables;
        public string financials;
        public string adunits;
        public ReportOptions(string _source, string _viewables, string _financials, string _adunits)
        {
            source = _source;
            viewables = _viewables;
            financials = _financials;
            adunits = _adunits;
        }
    }
}
