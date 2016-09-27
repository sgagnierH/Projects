using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tc.TcMedia.Scheduler;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System.Data.SqlClient;
using Tc.TcMedia.Sfo;

namespace GENReportScheduler
{
    public class GENReportScheduler : iScheduler
    {
        Dictionary<string, long>Templates = new Dictionary<string,long>();
        Dictionary<long, long>OrderIds = new Dictionary<long, long>();

        public void Run(Process process)
        {
            Command cmd = JsonConvert.DeserializeObject<Command>(process.schedule.Config);
            DateTime today = process.schedule.NextRun.Date;
            CheckReportingData(process, today);

            MySqlDataReader dataReader = null;
            Dictionary<int,ReportOptions>reportOptions = new Dictionary<int,ReportOptions>();  
            reportOptions.Add(0, new ReportOptions("DFP","0","0","0"));
            reportOptions.Add(1, new ReportOptions("APN","0","0","1"));

            try
            {
                // Get report templates.
                dataReader = Db.getMySqlReader(process, "SELECT * FROM 0_report_templates");
                Templates.Clear();
                while (dataReader.Read())
                {
                    Templates.Add(dataReader.GetString("reportTemplateName"), dataReader.GetInt64("reportTemplateId"));
                }
                dataReader.Close();

                for(int i = 0; i < reportOptions.Count; i++)
                {
                    ReportOptions options = reportOptions[i];
                    string viewName = options.source.ToLower();

                    // ended take all orders
                    processOrders(process, "SELECT * FROM " + viewName + "_orders_v_pdf_report_all WHERE endDateTime BETWEEN '" + Db.toMySqlDate(today.AddDays(-1)) + "' AND '" + Db.toMySqlDate(today.AddSeconds(-1)) + "'", cmd.end, "END", options, today);

                    // monthly
                    processOrders(process, "SELECT * FROM " + viewName + "_orders_v_pdf_report WHERE notes like '%{%pdf_reporting%MONTHLY:" + today.Day + "%'", cmd.monthly, "MONTHLY", options, today);

                    // weekly    
                    processOrders(process, "SELECT * FROM " + viewName + "_orders_v_pdf_report WHERE notes like '%{%pdf_reporting%WEEKLY:" + today.DayOfWeek + "%'", cmd.weekly, "WEEKLY", options, today);

                    // daily
                    processOrders(process, "SELECT * FROM " + viewName + "_orders_v_pdf_report WHERE notes like '%{%pdf_reporting%DAILY%'", cmd.daily, "DAILY", options, today);
                }
            }
            catch(Exception ex)
            {
                throw new Exception("Unable to schedule reports", ex);
            }
        }

        public void processOrders(Process process, string sql, string defaultTemplate, string occurence, ReportOptions options, DateTime date)
        {
            MySqlDataReader dataReader = null;
            try
            {
                dataReader = Db.getMySqlReader(process, sql);
                while (dataReader.Read())
                {
                    long orderId = dataReader.GetInt64("orderId");
                    if (!OrderIds.ContainsKey(orderId))
                    {
                        PDFReport rpt = getJsonFromNote(dataReader.GetString("notes"), defaultTemplate);

                        // Processing emails
                        string emails = rpt.emails;

                        if (rpt.lang == null)
                            rpt.lang = dataReader.GetString("reportLanguage");

                        string traffickerEmail = dataReader.GetString("traffickerEmail");
                        if (traffickerEmail == "ND") traffickerEmail = "";

                        string salespersonEmail = dataReader.GetString("salespersonEmail");
                        if (salespersonEmail == "ND") salespersonEmail = "";

                        emails = emails.Replace("%trafficker%", traffickerEmail).Replace("%salesperson%", salespersonEmail).Replace(",,", ",");

                        // Processing template
                        string template = rpt.template;
                        if (template == "default" || template == null) template = defaultTemplate;
                        template = template.Replace("%occurence%", occurence).Replace("%advertiser%", dataReader.GetInt64("advertiserId").ToString()).Replace("%order%", dataReader.GetInt64("orderId").ToString());
                        if (!Templates.ContainsKey(template))
                            throw new Exception("Template " + template + "not found");

                        // Setting email title
                        string title = rpt.title;
                        if (title == "default" || title == null) title = Db.getConfig("pdf_reporting_title_" + occurence + rpt.lang, "");
                        title = (title == null || title == "") ? occurence + " - " + dataReader.GetString("name") : title.Replace("%occurence%", occurence).Replace("%name%", dataReader.GetString("name"));
                        title = title.Replace("'", "''");

                        // Setting email body
                        string body = rpt.body;
                        if (body == "default" || body == null) body = Db.getConfig("pdf_reporting_body_" + occurence + rpt.lang, "");
                        body = (body == null || body == "") ? occurence + " - " + dataReader.GetString("name") : body.Replace("%occurence%", occurence).Replace("%name%", dataReader.GetString("name"));
                        body = body.Replace("'", "''");

                        // Add the report
                        if (!Db.alreadyInReportQueue(process, dataReader.GetInt64("orderId"), date, options.source))
                            Db.execSqlCommand(process, "INSERT INTO 0_report_queue (reportTemplateId, date, orderId, source, options, emails, lang, title, body) VALUES (" + Templates[template] + ",'" + Db.toMySqlDate(date) + "'," + dataReader.GetInt64("orderId") + ",'" + options.source + "','" + JsonConvert.SerializeObject(options) + "','" + emails + "','" + rpt.lang + "','" + title + "','" + body + "')");
                        OrderIds.Add(orderId, orderId);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                dataReader.Close();
            }
        }
        void CheckReportingData(Process process, System.DateTime date)
        {
            bool missDfp = false;
            bool missApn = false;
            MySqlDataReader dataReader = null;
            process.log("Checking Reporting Data from DFP");
            
            dataReader = Db.getMySqlReader(process, "select count(*) as nb from dfp_report_historical_data where date = '" + Db.toMySqlDate(date.AddDays(-1)) + "'");
            dataReader.Read();
            if (dataReader.GetInt64(0).Equals(0))
                missDfp = true;
            dataReader.Close();

            process.log("Checking Reporting Data from APN");
            dataReader = Db.getMySqlReader(process, "select count(*) as nb from apn_report_historical_data where date = '" + Db.toMySqlDate(date.AddDays(-1)) + "'");
            dataReader.Read();
            if (dataReader.GetInt64(0).Equals(0))
                missApn = true;
            dataReader.Close();

            if(missDfp || missApn)
            {
                Db.sendError(process, "Missing " + ((missDfp && missApn) ? "DFP and APN" : (missDfp) ? "DFP" : "APN") + " data");
                process.log("Missing " + ((missDfp && missApn) ? "DFP and APN" : (missDfp) ? "DFP" : "APN") + " data ... sleeping");
                System.Threading.Thread.Sleep(300000);
                throw new Exception("Missing " + ((missDfp && missApn) ? "DFP and APN" : (missDfp) ? "DFP" : "APN") + " data");
            }
        }
        PDFReport getJsonFromNote(string input, string defaultTemplate)
        {
            if (input.IndexOf("{") < 0)
            {
                return new PDFReport();
            }

            input = input.Substring(input.IndexOf("{"));
            input = input.Substring(0, input.IndexOf("}") + 1);
            return JsonConvert.DeserializeObject<PDFReport>(input);
        }
    }
    class Command
    {
        public string end { get; set;}
        public string daily { get; set; }
        public string weekly { get; set; }
        public string monthly { get; set; }
    }
    class PDFReport
    {
        public string pdf_reporting { get; set; }
        public string emails { get; set; }
        public string template { get; set; }
        public string lang { get; set; }
        public string title { get; set; }
        public string body { get; set; }

        public PDFReport()
        {
            emails = Db.getConfig("defaultReportEmails", "tcapiaccess@tc.tc");
        }
    }
}
