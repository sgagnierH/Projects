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

namespace GENReportSpecifics
{
    public class GENReportSpecifics : iScheduler
    {
        Dictionary <string, long>Templates = new Dictionary<string,long>();
        Dictionary<long, long>OrderIds = new Dictionary<long, long>();

        public void Run(Process process)
        {
            Command cmd = JsonConvert.DeserializeObject<Command>(process.schedule.Config);
            DateTime today = DateTime.Now.Date;

            try
            {
                // Get report templates.
                MySqlDataReader dataReader = Db.getMySqlReader(process, "SELECT * FROM 0_report_templates");
                while (dataReader.Read())
                {
                    Templates.Add(dataReader.GetString("reportTemplateName"), dataReader.GetInt64("reportTemplateId"));
                }
                dataReader.Close();

                // ended take all orders
                processOrders(process, "SELECT * FROM dfp_orders_v_pdf_report_all where orderId in (" + cmd.orderIds + ")", cmd.template, "END", cmd.emails, cmd);
            }
            catch(Exception ex)
            {
                throw new Exception("Unable to schedule reports", ex);
            }
        }

        public void processOrders(Process process, string sql, string defaultTemplate, string occurence, string emails, Command cmd)
        {
 
            MySqlDataReader dataReader = Db.getMySqlReader(process, sql);
            while (dataReader.Read())
            {
                long orderId = dataReader.GetInt64("orderId");
                if (!OrderIds.ContainsKey(orderId))
                {
                    Report rpt = getJsonFromNote(dataReader.GetString("notes"), defaultTemplate);

                    // Processing emails
                    if (emails == null || emails == "")
                        emails = rpt.emails;
					
					if(cmd.lang != ""){
						rpt.lang = cmd.lang;
						/* Dans l'interface qui génère les rapports pour les adops. 
						   Ça serait bien qu'ils puissent forcer la langue, ainsi s'ils en ont besoin d'un en français, ils peuvent le faire.
						   Ça pourrait même changer la valeur pour cette compagnie là.
						*/
					}
                    if(rpt.lang == null)
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
                    if(body == "default" || body == null) body = Db.getConfig("pdf_reporting_body_" + occurence + rpt.lang, "");
                    body = (body == null || body == "") ? occurence + " - " + dataReader.GetString("name") : body.Replace("%occurence%", occurence).Replace("%name%", dataReader.GetString("name"));
                    body = body.Replace("'", "''");

                    // Add the report
                    Db.execSqlCommand(process, "INSERT INTO 0_report_queue (reportTemplateId, orderId, emails, lang, title, body) VALUES (" + Templates[template] + "," + dataReader.GetInt64("orderId") + ",'" + emails + "','" + rpt.lang + "','" + title + "','" + body + "')");
                    OrderIds.Add(orderId, orderId);
                }
            }
            dataReader.Close();
        }
        Report getJsonFromNote(string input, string defaultTemplate)
        {
            if (input.IndexOf("{") < 0)
            {
                return new Report();
            }

            input = input.Substring(input.IndexOf("{"));
            input = input.Substring(0, input.IndexOf("}") + 1);
            return JsonConvert.DeserializeObject<Report>(input);
        }
    }
    public class Command
    {
        public string orderIds { get; set;}
        public string template { get; set;}
        public string emails { get; set; }
        public string lang { get; set; }
    }
    class Report
    {
        public string pdf_reporting { get; set; }
        public string emails { get; set; }
        public string template { get; set; }
        public string lang { get; set; }
        public string title { get; set; }
        public string body { get; set; }

        public Report()
        {
            emails = Db.getConfig("defaultReportEmails", "tcapiaccess@tc.tc");
        }
    }
}
