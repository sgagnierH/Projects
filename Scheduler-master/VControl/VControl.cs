using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Tc.TcMedia.Scheduler.Classes;
using Tc.TcMedia.Apn;
using MySql.Data.MySqlClient;
using Ecrion.Ultrascale;

namespace Tc.TcMedia.Scheduler
{
    public partial class VControl : Form
    {
        bool closingMessageDisplayed = false;

        public VControl()
        {
            InitializeComponent();
        }

        private void processing(Process process)
        {
            while (clsGlobal.g_Running)
            {
                DateTime startDateTime = System.DateTime.MinValue;
                DateTime endDateTime = System.DateTime.MinValue;
                bool Success = false; 
                
                try
                {
                    process.schedule = Db.getNextSchedule(process);

                    if (process.schedule == null)
                    {
                        process.setTitle("Sleep " + System.DateTime.Now.ToString("HH:mm:ss"), false);
                        System.Threading.Thread.Sleep(10000);
                    }
                    else
                    {
                        string path = Assembly.GetExecutingAssembly().Location.Replace("VControl.exe", "");
                        string dllPath = path + process.schedule.Name + ".dll";
                        if (!File.Exists(dllPath))
                            dllPath = path.Replace("VControl", process.schedule.Name) + process.schedule.Name + ".dll";

                        Assembly assembly = Assembly.LoadFile(dllPath);
                        Type theType = assembly.GetType(process.schedule.Name + "." + process.schedule.Name);
                        process.setTitle(System.DateTime.Now.ToString("HH:mm:ss ") + process.schedule.Name);
                        process.log("----------------");
                        process.log(process.schedule.Name);
                        process.log("----------------");
                        iScheduler c = (iScheduler)Activator.CreateInstance(theType, null);
                        process.setTitle(process.schedule.Name);

                        startDateTime = System.DateTime.Now;
                        c.Run(process);
                        endDateTime = System.DateTime.Now;

                        Success = true;
                        process.log("================");
                        process.log(process.schedule.Name + " completed");
                        process.log("================");
                        Db.setSuccess(process);
                    }
                }
                catch(APNApiException ex)
                {
                    endDateTime = System.DateTime.Now;
                    process.log("================");
                    process.log(process.schedule.Name + " ERROR - sleeping");
                    process.log(ex.Message);
                    process.log(ex.StackTrace);
                    process.log("================");
                    System.Threading.Thread.Sleep(120000);
                    Db.setFailure(process, (ex.Message.Length < 200) ? ex.Message : ex.Message.Substring(0, 199));
                }
                catch (Exception ex)
                {
                    endDateTime = System.DateTime.Now;
                    process.log("================");
                    process.log(process.schedule.Name + " ERROR");
                    process.log(ex.Message);
                    process.log(ex.StackTrace);
                    process.log("================");
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
                }
                finally
                {
                    if(process.schedule != null)
                        Db.execSqlCommand(process, "INSERT INTO 0_execution_log (scheduleId, startDateTime, endDateTime, inDaySchedule, success) VALUES (" + process.schedule.ScheduleId + ",'" + Db.toMySqlDate(startDateTime) + "','" + Db.toMySqlDate(endDateTime) + "'," + ((process.inDaySchedule) ? "1" : "0") + "," + ((Success) ? "1" : "0") + ")");
                    process.conn.Close();
                }
            }
            MethodInvoker ldispose = delegate { process.listbox.Dispose(); };
            process.listbox.Invoke(ldispose);
            MethodInvoker pdispose = delegate { process.tabpage.Dispose(); };
            process.tabpage.Invoke(pdispose);
            clsGlobal.g_Processes.Remove(process.guid);                
        }
        private void reporting(Process process)
        {
            Thread.Sleep(2000);
            int HeartBeatDelay = Convert.ToInt16(System.Configuration.ConfigurationManager.AppSettings["HeartBeatDelay"]);
            System.DateTime hb = System.DateTime.Now.AddMinutes(HeartBeatDelay);
            XmlDocument xDoc; XmlElement xRoot; XmlElement xElem; XmlElement xSub; XmlElement xSub2; XmlElement xOrder;
            XmlDocument xLanguage = new XmlDocument(); XmlElement Language;
            bool hadReport = false;
            MySqlDataReader dr = null;

            string inFolder = System.Configuration.ConfigurationManager.AppSettings["inFolder"];
            if (!Directory.Exists(inFolder)) Directory.CreateDirectory(inFolder);
            string errorFolder = inFolder.Replace("in", "error"); if (!Directory.Exists(errorFolder)) Directory.CreateDirectory(errorFolder);
            string doneFolder = inFolder.Replace("in", "done"); if (!Directory.Exists(doneFolder)) Directory.CreateDirectory(doneFolder);

            string xmlFolder = System.Configuration.ConfigurationManager.AppSettings["xmlFolder"];
            if (!Directory.Exists(xmlFolder)) Directory.CreateDirectory(xmlFolder);

            string outputFolder = System.Configuration.ConfigurationManager.AppSettings["outputFolder"];
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            string foFolder = System.Configuration.ConfigurationManager.AppSettings["foFolder"];
            if (!Directory.Exists(foFolder)) throw new Exception("Fo folder not found");

            bool localTransform = (System.Configuration.ConfigurationManager.AppSettings["localTransform"] == "0") ? false : true;

            string transformPath = System.Configuration.ConfigurationManager.AppSettings["transformPath"];
            //if (!File.Exists(transformPath) && localTransform) throw new Exception("Transform not found");
            
            while (clsGlobal.g_Running)
            {
                try
                {
                    xDoc = new XmlDocument();
                    Report report = Db.getReport(process);
                    if (report == null)
                    {
                        #region generate pdf
                        // No report to process
                        if (hadReport)
                        {
                            process.log("No report to process");
                            hadReport = false;
                        }

                        process.setTitle(System.DateTime.Now.ToString("HH:mm:ss ") + "Reporting");

                        if (localTransform)
                        {
                            foreach (string file in Directory.EnumerateFiles(inFolder))
                            {
                                if (file.EndsWith(".xml"))
                                {
                                    xDoc.Load(file);
                                    process.log("Generating pdf file " + file);

                                    if (renderIt(file, foFolder + xDoc.FirstChild.Attributes["xfd"].Value, outputFolder + xDoc.FirstChild.Attributes["pdfFileName"].Value + ".pdf"))
                                        File.Move(file, doneFolder + file.Substring(file.LastIndexOf("\\") + 1));
                                    else
                                        File.Move(file, errorFolder + file.Substring(file.LastIndexOf("\\") + 1));
                                }
                            }
                        }

                        System.Threading.Thread.Sleep(2000);
                        #endregion
                    }
                    else
                    {
                        #region createReport
                        #region header
                        hadReport = true;
                        DateTime now = System.DateTime.Now;
                        string filename = "Rpt-" +Db.MakeValidFileName(report.advertiser + "-" + report.name);
                        string CompletionPCT = "0";
                        long totalImpressions = 0;
                        long totalClicks = 0;
                        long totalViewable = 0;
                        long unitsBought = 0;
                        long goalAct = 0;
                        int nbitems = 0;
                        process.setTitle(System.DateTime.Now.ToString("HH:mm:ss ") + "Reporting " + process.report.reportQueueId);

                        // Start building XML
                        xRoot = xDoc.CreateElement("data");
                        xRoot.SetAttribute("viewables", report.options.viewables);
                        xRoot.SetAttribute("financials", report.options.financials);
                        xRoot.SetAttribute("adunits", report.options.adunits);
                        xRoot.SetAttribute("xfd", report.reportTemplateName + ".xfd");
                        xRoot.SetAttribute("pdfFileName", filename);
                        xDoc.AppendChild(xRoot);

                        #region Language
                        // Language
                        process.log(process.report.reportQueueId + " - Writing Language tag");
                        xLanguage.Load(foFolder + report.reportTemplateName + "_lang.xml");
                        Language = xDoc.CreateElement("Language");
                        Language.SetAttribute("lang", report.lang);
                        Language.InnerXml = xLanguage.SelectSingleNode("Language").InnerXml;
                        xRoot.AppendChild(Language);
                        if (report.lang == "fr")
                            xRoot.SetAttribute("title", xLanguage.SelectSingleNode("/Language/Fields/perfReport/@fr").InnerText);
                        else
                            xRoot.SetAttribute("title", xLanguage.SelectSingleNode("/Language/Fields/perfReport/@en").InnerText);
                        #endregion
                        
                        xOrder = xDoc.CreateElement("order");
                        xRoot.AppendChild(xOrder);
                        xRoot.SetAttribute("GeneratedDate", formatDate(now, report.lang, xLanguage));
                        xRoot.SetAttribute("GeneratedTime", now.ToString("HH:mm:ss") + " UTC");

                        xOrder.SetAttribute("SalesForceOrder", report.orderNo);
                        xOrder.SetAttribute("orderId", report.orderId.ToString());
                        xOrder.SetAttribute("advertiser", report.advertiser);
                        xOrder.SetAttribute("name", report.name);
                        xOrder.SetAttribute("startDateTime", formatDate(report.startDateTime, report.lang, xLanguage));
                        if (!report.unlimitedEndDateTime)
                            xOrder.SetAttribute("endDateTime", formatDate(report.endDateTime, report.lang, xLanguage));
                        if (report.poNumber != null) xOrder.SetAttribute("poNumber", report.poNumber.ToString());
                        xOrder.SetAttribute("status", report.status);
                        xOrder.SetAttribute("totalBudget", report.totalBudget.ToString());
                        xOrder.SetAttribute("salespersonId", report.salespersonId.ToString());
                        xOrder.SetAttribute("salespersonName", report.salespersonName);
                        xOrder.SetAttribute("salespersonEmail", report.salespersonEmail);

                        int orderDays = 0;
                        DateTime orderStartDate = DateTime.MaxValue;
                        DateTime orderEndDate = DateTime.MaxValue;
                        try
                        {
                            orderStartDate = report.startDateTime;
                            orderEndDate = report.endDateTime;
                            orderDays = Convert.ToInt16(orderEndDate.Subtract(orderStartDate).TotalDays);
                        }
                        catch (Exception) { }

                        #endregion

                        #region stats
                        #region adunits
                        // Get AdUnits data
                        process.log(process.report.reportQueueId + " - AdUnits");
                        dr = Db.getMySqlReader(process, "SELECT url, sum(totalImpressions) as totalImpressions FROM " + report.source.ToLower() + "_report_historical_data_v_adunit WHERE rating>=5 AND orderId = " + report.orderId + " GROUP BY url ORDER BY sum(totalImpressions) DESC");
                        xElem = xDoc.CreateElement("adunits");
                        xRoot.AppendChild(xElem);
                        totalImpressions = 0;
                        nbitems = 0;

                        while (dr.Read())
                        {
                            nbitems++;
                            xSub = xDoc.CreateElement("row");
                            //xSub.SetAttribute("adunitid", dr.GetInt64("adunitId").ToString());
                            //xSub.SetAttribute("adunit", dr.GetString("adunit"));
                            xSub.SetAttribute("url", dr.GetString("url"));
                            xSub.SetAttribute("impressions", dr.GetInt64("totalImpressions").ToString());
                            xSub.SetAttribute("pieImpressions", (nbitems > 10) ? "" : dr.GetInt64("totalImpressions").ToString());
                            xElem.AppendChild(xSub);
                            totalImpressions += dr.GetInt64("totalImpressions");
                        }
                        xElem.SetAttribute("totalImpressions", totalImpressions.ToString());
                        dr.Close();                       
                        #endregion

                        #region lineitems
                        // Get Lineitems data
                        process.log(process.report.reportQueueId + " - Lineitems");
                        dr = Db.getMySqlReader(process, "SELECT lineitemId, lineitemName, startDateTime, endDateTime, unlimitedEndDateTime, sum(totalImpressions) as totalImpressions, sum(totalClicks) as totalClicks, sum(totalActiveViewViewableImpressions) as totalActiveViewViewableImpressions, contractedUnitsBought, min(primaryGoal_unitType) as primaryGoal_unitType, min(`status`) as `status`, budget, min(costType) as costType FROM " + report.source.ToLower() + "_report_historical_data_v_lineitem WHERE orderId = " + report.orderId + " GROUP BY lineitemId, lineitemName, contractedUnitsBought, budget ORDER BY unlimitedEndDateTime DESC, endDateTime ASC, sum(totalImpressions) DESC");
                        xElem = xDoc.CreateElement("lineitems");
                        xRoot.AppendChild(xElem);

                        CompletionPCT = "0";
                        totalImpressions = 0;
                        totalClicks = 0;
                        totalViewable = 0;
                        unitsBought = 0;
                        goalAct = 0;
                        nbitems = 0;
                        DateTime minStartDate = DateTime.MaxValue;
                        DateTime maxEndDate = DateTime.MinValue;
                        DateTime today = DateTime.Now.Date;

                        while (dr.Read())
                        {
                            nbitems++; 
                            xSub = xDoc.CreateElement("row");
                            xSub.SetAttribute("lineitemid", dr.GetInt64("lineitemId").ToString());
                            xSub.SetAttribute("lineitemName", dr.GetString("lineitemName"));

                            xSub.SetAttribute("startDateTime", formatDate(dr.GetDateTime("startDateTime"), report.lang, xLanguage));
                            if (!dr.GetBoolean("unlimitedEndDateTime"))
                                xSub.SetAttribute("endDateTime", formatDate(dr.GetDateTime("endDateTime"), report.lang, xLanguage));

                            if (minStartDate > dr.GetDateTime("startDateTime")) minStartDate = dr.GetDateTime("startDateTime");
                            if (!dr.GetBoolean("unlimitedEndDateTime"))
                                if (maxEndDate < dr.GetDateTime("endDateTime")) maxEndDate = dr.GetDateTime("endDateTime");

                            if (dr.GetDateTime("startDateTime") > today)
                                xSub.SetAttribute("status", xLanguage.SelectSingleNode("/Language/Fields/waiting/@" + report.lang).InnerText);
                            else if (dr.GetBoolean("unlimitedEndDateTime"))
                                xSub.SetAttribute("status", xLanguage.SelectSingleNode("/Language/Fields/delivering/@" + report.lang).InnerText);
                            else if (dr.GetDateTime("endDateTime") < today)
                            {
                                xSub.SetAttribute("status", xLanguage.SelectSingleNode("/Language/Fields/completed/@" + report.lang).InnerText);
                                xSub.SetAttribute("completed", "1");
                            }
                            else
                                xSub.SetAttribute("status", xLanguage.SelectSingleNode("/Language/Fields/delivering/@" + report.lang).InnerText);
                            
                            xSub.SetAttribute("impressions", dr.GetInt64("totalImpressions").ToString());
                            totalImpressions += dr.GetInt64("totalImpressions");
                            xSub.SetAttribute("clicks", dr.GetInt64("totalClicks").ToString());
                            totalClicks += dr.GetInt64("totalClicks");
                            xSub.SetAttribute("ctr", getPct(dr.GetInt64("totalClicks"),dr.GetInt64("totalImpressions")));
                            xSub.SetAttribute("ViewableImpressions", dr.GetInt64("totalActiveViewViewableImpressions").ToString());
                            totalViewable += dr.GetInt64("totalActiveViewViewableImpressions");
                            xSub.SetAttribute("primaryGoal_unitType", dr.GetString("primaryGoal_unitType"));
                            int nbDays = 0;
                            if(dr.GetBoolean("unlimitedEndDateTime"))
                                nbDays = Convert.ToInt16(System.DateTime.Now.Date.Subtract(dr.GetDateTime("startDateTime").Date.AddDays(-1)).TotalDays);
                            else
                                nbDays = Convert.ToInt16(dr.GetDateTime("endDateTime").Date.Subtract(dr.GetDateTime("startDateTime").Date.AddDays(-1)).TotalDays);
                            int daysDone = Convert.ToInt32(System.DateTime.Now.Date.Subtract(dr.GetDateTime("startDateTime").Date.AddDays(-1)).TotalDays);
                            switch (dr.GetString("costType"))
                            {
                                case "CPD":
                                    xSub.SetAttribute("contractedUnitsBought", nbDays.ToString());
                                    unitsBought += nbDays;
                                    if (dr.GetBoolean("unlimitedEndDateTime"))
                                    {
                                        CompletionPCT = (daysDone * 100 / nbDays).ToString();
                                        goalAct += daysDone;
                                        xSub.SetAttribute("unitsDelivered", daysDone.ToString());
                                    }
                                    else if (System.DateTime.Now.Date > dr.GetDateTime("endDateTime"))
                                    {
                                        CompletionPCT = "100";
                                        goalAct += nbDays;
                                        xSub.SetAttribute("unitsDelivered", nbDays.ToString());
                                    }
                                    else
                                    {
                                        CompletionPCT = (daysDone * 100 / nbDays).ToString();
                                        goalAct += daysDone;
                                        xSub.SetAttribute("unitsDelivered", daysDone.ToString());
                                    }
                                    break;

                                case "CPM":
                                    xSub.SetAttribute("contractedUnitsBought", dr.GetInt64("contractedUnitsBought").ToString());
                                    unitsBought += dr.GetInt64("contractedUnitsBought");
                                    if (dr.GetInt64("contractedUnitsBought") > 0)
                                        CompletionPCT = getPct(dr.GetInt64("totalImpressions"), dr.GetInt64("contractedUnitsBought"));
                                    else
                                        CompletionPCT = (daysDone * 100 / nbDays).ToString();

                                    if (dr.GetInt64("totalImpressions") == 0)
                                        CompletionPCT = "0";

                                    xSub.SetAttribute("unitsDelivered", dr.GetInt64("totalImpressions").ToString());
                                    goalAct += dr.GetInt64("totalImpressions");
                                    break;

                                case "CPC":
                                    xSub.SetAttribute("contractedUnitsBought", dr.GetInt64("contractedUnitsBought").ToString());
                                    unitsBought += dr.GetInt64("contractedUnitsBought"); 
                                    if (dr.GetInt64("contractedUnitsBought") > 0)
                                        CompletionPCT = getPct(dr.GetInt64("totalClicks"), dr.GetInt64("contractedUnitsBought"));
                                    else
                                        CompletionPCT = (daysDone * 100 / nbDays).ToString();      

                                    xSub.SetAttribute("unitsDelivered", dr.GetInt64("totalClicks").ToString());
                                    goalAct += dr.GetInt64("totalClicks");
                                    break;
                            }

                            xSub.SetAttribute("budget", Convert.ToString(dr.GetInt64("budget")));
                            xSub.SetAttribute("costType", dr.GetString("costType"));
                            xSub.SetAttribute("completionPct", CompletionPCT);
                            xSub.SetAttribute("actualSpent", Convert.ToString(Convert.ToDecimal(CompletionPCT) * dr.GetInt64("budget")));
                            xSub.SetAttribute("viewablePct", getPct(dr.GetInt64("totalActiveViewViewableImpressions"), dr.GetInt64("totalImpressions")));
                            //xSub.SetAttribute("status", dr.GetString("status"));
                            xSub.SetAttribute("pieImpressions", (nbitems > 10) ? "" : dr.GetInt64("totalImpressions").ToString());
                            xElem.AppendChild(xSub);
                        }
                        dr.Close();

                        xElem.SetAttribute("totalImpressions", totalImpressions.ToString());
                        xElem.SetAttribute("totalClicks", totalClicks.ToString());
                        xElem.SetAttribute("totalBought", unitsBought.ToString());

                        if (unitsBought > 0)
                            xElem.SetAttribute("completionPct", getPct(goalAct, unitsBought));
                        else if (System.DateTime.Now.Date > maxEndDate)
                            xElem.SetAttribute("completionPct", "100");
                        else
                        {
                            long totalDays = Convert.ToInt16(maxEndDate.Subtract(minStartDate.AddDays(-1)).TotalDays);
                            int daysDone = Convert.ToInt32(System.DateTime.Now.Date.Subtract(minStartDate.Date.AddDays(-1)).TotalDays);
                            xElem.SetAttribute("completionPct", (daysDone * 100 / totalDays).ToString());
                        }
                        
                        xElem.SetAttribute("totalViewable", totalViewable.ToString());
                        xElem.SetAttribute("viewablePct", getPct(totalViewable, totalImpressions));
                        xElem.SetAttribute("clickThroughRate", getPct(totalClicks, totalImpressions));

                        xOrder.SetAttribute("totalImpressions", totalImpressions.ToString());
                        xOrder.SetAttribute("totalClicks", totalClicks.ToString());
                        xOrder.SetAttribute("totalBought", unitsBought.ToString());
                        xOrder.SetAttribute("completionPct", getPct(goalAct, unitsBought));
                        xOrder.SetAttribute("totalViewable", totalViewable.ToString());
                        xOrder.SetAttribute("viewablePct", getPct(totalViewable, totalImpressions));
                        xOrder.SetAttribute("clickThroughRate", getPct(totalClicks, totalImpressions));
                        #endregion

                        #region creatives
                        process.log(process.report.reportQueueId + " - Creatives");
                        foreach (XmlNode li in xDoc.SelectNodes("/data/lineitems/row"))
                        {
                            xSub = null;
                            string currentCreative = "";
                            nbitems = 0;
                            dr = Db.getMySqlReader(process, "SELECT date, creativeId, creativeName, creativeSize, sum(totalImpressions) as totalImpressions, sum(totalClicks) as totalClicks, sum(totalActiveViewViewableImpressions) as totalActiveViewViewableImpressions FROM " + report.source.ToLower() + "_report_historical_data_v_creative WHERE lineitemId=" + li.Attributes["lineitemid"].Value + " GROUP BY lineitemId, creativeId, creativeName, creativeSize, date ORDER BY lineitemId, creativeId, creativeName, creativeSize, date DESC");
                            while (dr.Read())
                            {
                                if (currentCreative != dr.GetInt64("creativeId")+dr.GetString("creativeSize"))
                                {
                                    currentCreative = dr.GetInt64("creativeId") + dr.GetString("creativeSize");
                                    if (xSub != null) //First pass, it doesn't exists.
                                    {
                                        xSub.SetAttribute("viewablePct", getPct(totalViewable, totalImpressions));
                                        xSub.SetAttribute("clickThroughRate", getPct(totalClicks, totalImpressions));
                                    }
                                    totalImpressions = 0;
                                    totalClicks = 0;
                                    totalViewable = 0;
                                    nbitems = 0;

                                    xSub = xDoc.CreateElement("creative");
                                    xSub.SetAttribute("name", dr.GetString("creativeName").Replace("&#0;",""));
                                    xSub.SetAttribute("creativeId", dr.GetInt64("creativeId").ToString());
                                    xSub.SetAttribute("size", dr.GetString("creativeSize"));
                                    li.AppendChild(xSub);
                                }

                                if (++nbitems > 31)
                                {
                                    if (nbitems == 32)
                                        xSub.SetAttribute("limited", "30");
                                }
                                else
                                {
                                    xSub2 = xDoc.CreateElement("row");
                                    if (xSub.HasChildNodes)
                                        xSub.InsertBefore(xSub2, xSub.FirstChild);
                                    else
                                        xSub.AppendChild(xSub2);

                                    xSub2.SetAttribute("date", formatDate(dr.GetDateTime("date"), report.lang, xLanguage));
                                    xSub2.SetAttribute("impressions", dr.GetInt64("totalImpressions").ToString());
                                    totalImpressions += dr.GetInt64("totalImpressions");
                                    xSub2.SetAttribute("clicks", dr.GetInt64("totalClicks").ToString());
                                    totalClicks += dr.GetInt64("totalClicks");
                                    xSub2.SetAttribute("ViewableImpressions", dr.GetInt64("totalActiveViewViewableImpressions").ToString());
                                    totalViewable += dr.GetInt64("totalActiveViewViewableImpressions");
                                    xSub2.SetAttribute("viewablePct", getPct(dr.GetInt64("totalActiveViewViewableImpressions"), dr.GetInt64("totalImpressions")));
                                }

                            }
                            dr.Close();

                            if (xSub != null) // No Creatives
                            {
                                xSub.SetAttribute("viewablePct", getPct(totalViewable, totalImpressions));
                                xSub.SetAttribute("clickThroughRate", getPct(totalClicks, totalImpressions));
                            }
                        }
                        #endregion

                        #region monthly
                        // View by month
                        process.log(process.report.reportQueueId + " - Monthly");
                        dr = Db.getMySqlReader(process, "SELECT ym, sum(totalImpressions) as totalImpressions, sum(totalClicks) as totalClicks, sum(totalActiveViewViewableImpressions) as totalActiveViewViewableImpressions FROM " + report.source.ToLower() + "_report_historical_data_v WHERE orderId = " + report.orderId + " GROUP BY ym ORDER BY ym ASC");
                        xElem = xDoc.CreateElement("months");
                        xRoot.AppendChild(xElem);

                        totalImpressions = 0;
                        totalClicks = 0;
                        totalViewable = 0;

                        while (dr.Read())
                        {
                            xSub = xDoc.CreateElement("row");
                            xSub.SetAttribute("ym", dr.GetInt64("ym").ToString());
                            string varName = xLanguage.SelectSingleNode("/Language/Months/Month[position()=" + Convert.ToInt16(dr.GetInt64("ym") % 100) + "]/@" + report.lang.ToString()).Value + " " + dr.GetInt64("ym").ToString().Substring(0, 4);
                            xSub.SetAttribute("name", varName);
                            xSub.SetAttribute("impressions", dr.GetInt64("totalImpressions").ToString());
                            totalImpressions += dr.GetInt64("totalImpressions");
                            xSub.SetAttribute("clicks", dr.GetInt64("totalClicks").ToString());
                            totalClicks += dr.GetInt64("totalClicks");
                            xSub.SetAttribute("ViewableImpressions", dr.GetInt64("totalActiveViewViewableImpressions").ToString());
                            totalViewable += dr.GetInt64("totalActiveViewViewableImpressions");
                            xSub.SetAttribute("viewablePct", getPct(dr.GetInt64("totalActiveViewViewableImpressions"), dr.GetInt64("totalImpressions")));
                            xElem.AppendChild(xSub);
                        }
                        dr.Close();
                        #endregion

                        #region daily
                        // View by day
                        process.log(process.report.reportQueueId + " - Daily");
                        dr = Db.getMySqlReader(process, "SELECT date, sum(totalImpressions) as totalImpressions, sum(totalClicks) as totalClicks, sum(totalActiveViewViewableImpressions) as totalActiveViewViewableImpressions FROM " + report.source.ToLower() + "_report_historical_data_v WHERE orderId = " + report.orderId + " GROUP BY date ORDER BY date ASC");
                        xElem = xDoc.CreateElement("days");
                        xRoot.AppendChild(xElem);

                        totalImpressions = 0;
                        totalClicks = 0;
                        totalViewable = 0;
                        nbitems = 0;
                        DateTime curDate = DateTime.MinValue;

                        while (dr.Read())
                        {
                            if (!curDate.Equals(DateTime.MinValue))
                            {
                                curDate = curDate.AddDays(1);
                                while (!curDate.Equals(dr.GetDateTime("date")))
                                {
                                    xSub = xDoc.CreateElement("row");
                                    xSub.SetAttribute("name", formatDayMonth(curDate, report.lang, xLanguage));
                                    xSub.SetAttribute("impressions", "0");
                                    xSub.SetAttribute("clicks", "0");
                                    xElem.AppendChild(xSub);
                                    curDate = curDate.AddDays(1);
                                }
                            }
                            curDate = dr.GetDateTime("date");

                            if (++nbitems > 31)
                            {
                                if (nbitems == 32)
                                    xElem.SetAttribute("limited", "30");
                            }
                            else
                            {
                                xSub = xDoc.CreateElement("row");
                                xSub.SetAttribute("name", formatDayMonth(curDate, report.lang, xLanguage));
                                xSub.SetAttribute("impressions", dr.GetInt64("totalImpressions").ToString());
                                totalImpressions += dr.GetInt64("totalImpressions");
                                xSub.SetAttribute("clicks", dr.GetInt64("totalClicks").ToString());
                                totalClicks += dr.GetInt64("totalClicks");
                                xSub.SetAttribute("ViewableImpressions", dr.GetInt64("totalActiveViewViewableImpressions").ToString());
                                totalViewable += dr.GetInt64("totalActiveViewViewableImpressions");
                                xSub.SetAttribute("viewablePct", getPct(dr.GetInt64("totalActiveViewViewableImpressions"), dr.GetInt64("totalImpressions")));
                                xElem.AppendChild(xSub);
                            }
                        }
                        dr.Close();
                        #endregion

                        #region creativeSize
                        // View by creative Size
                        process.log(process.report.reportQueueId + " - Creative size");
                        dr = Db.getMySqlReader(process, "SELECT date, creativeSize, sum(totalImpressions) as totalImpressions, sum(totalClicks) as totalClicks, sum(totalActiveViewViewableImpressions) as totalActiveViewViewableImpressions FROM " + report.source.ToLower() + "_report_historical_data_v_creative WHERE orderId = " + report.orderId + " GROUP BY creativeSize, date ORDER BY creativeSize ASC, date DESC");
                        xElem = xDoc.CreateElement("creativeSizes");
                        xRoot.AppendChild(xElem);

                        string currentSize = "";
                        xSub = null;
                        nbitems = 0;

                        while (dr.Read())
                        {
                            if (currentSize != dr.GetString("creativeSize"))
                            {
                                currentSize = dr.GetString("creativeSize");
                                if (xSub != null) //First pass, it doesn't exists.
                                {
                                    xSub.SetAttribute("viewablePct", getPct(totalViewable, totalImpressions));
                                    xSub.SetAttribute("clickThroughRate", getPct(totalClicks, totalImpressions));
                                }
                                totalImpressions = 0;
                                totalClicks = 0;
                                totalViewable = 0;
                                nbitems = 0;

                                xSub = xDoc.CreateElement("size");
                                xSub.SetAttribute("name", dr.GetString("creativeSize"));
                                xElem.AppendChild(xSub);
                            }

                            if (++nbitems > 31)
                            {
                                if (nbitems == 32)
                                    xSub.SetAttribute("limited", "30");
                            }
                            else
                            {
                                if (xSub == null)
                                {
                                    xSub = xDoc.CreateElement("size");
                                    xSub.SetAttribute("name", "creatives");
                                    xElem.AppendChild(xSub);
                                }
                                xSub2 = xDoc.CreateElement("row");
                                if (xSub.HasChildNodes)
                                    xSub.InsertBefore(xSub2, xSub.FirstChild);
                                else
                                    xSub.AppendChild(xSub2);

                                xSub2.SetAttribute("date", formatDate(dr.GetDateTime("date"), report.lang, xLanguage));
                                xSub2.SetAttribute("impressions", dr.GetInt64("totalImpressions").ToString());
                                totalImpressions += dr.GetInt64("totalImpressions");
                                xSub2.SetAttribute("clicks", dr.GetInt64("totalClicks").ToString());
                                totalClicks += dr.GetInt64("totalClicks");
                                xSub2.SetAttribute("ViewableImpressions", dr.GetInt64("totalActiveViewViewableImpressions").ToString());
                                totalViewable += dr.GetInt64("totalActiveViewViewableImpressions");
                                xSub2.SetAttribute("viewablePct", getPct(dr.GetInt64("totalActiveViewViewableImpressions"), dr.GetInt64("totalImpressions")));
                            }

                        }
                        dr.Close();

                        if (xSub != null) // No Creatives
                        {
                            xSub.SetAttribute("viewablePct", getPct(totalViewable, totalImpressions));
                            xSub.SetAttribute("clickThroughRate", getPct(totalClicks, totalImpressions));
                        }
                        #endregion
                        #endregion

                        #region xslfo
                        if (File.Exists(filename + ".xml")) File.Delete(filename + ".xml");
                        xDoc.Save(xmlFolder + filename + ".xml");

                        if (!localTransform)
                        {
                            process.log(process.report.reportQueueId + " - Waiting for generating server");
                            if (File.Exists(inFolder + filename + ".xml")) File.Delete(inFolder + filename + ".xml");
                            File.Move(xmlFolder + filename + ".xml", inFolder + filename + ".xml");
                            bool found = false;
                            int rounds = 0;

                            while (!found)
                            {
                                if(File.Exists(doneFolder + filename + ".pdf"))
                                {
                                    Db.setReportSuccess(process);
                                    found = true;
                                }
                                else if(File.Exists(errorFolder + filename + ".xml"))
                                {
                                    Db.setReportFailure(process, "Error producing PDF");
                                    found = true;
                                }
                                if(!found)
                                {
                                    Thread.Sleep(2000);
                                    rounds++;
                                    if(rounds == 60)
                                    {
                                        Db.setReportFailure(process, "Timeout producing PDF");
                                        found = true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            process.log(process.report.reportQueueId + " - Generating pdf file");

                            if (renderIt(xmlFolder + filename + ".xml", foFolder + report.reportTemplateName + ".xfd", outputFolder + filename + ".pdf"))
                            {
                                if (report.sfo_orderId != null)
                                {
                                    /* This deals with the fact that report need to be attached or not depending on weither they'll be extended or not.
                                     * Of course, we can't ask people to see if the repport is releivant or not.
                                     */
                                    bool attach = true;
                                    int attached = -1;
                                    bool completed = Convert.ToDecimal(xDoc.SelectSingleNode("//data/order").Attributes["completionPct"].Value) >= 100;
                                    if (!completed)
                                    {
                                        dr = Db.getMySqlReader(process, "SELECT attached FROM sfo_orders WHERE sfo_orderId = '" + report.sfo_orderId + "'");
                                        while (dr.Read())
                                            attached = dr.GetInt16("attached");
                                        dr.Close();
                                        var hier = DateTime.Now.AddDays(-1);
                                        if (hier.DayOfYear == report.endDateTime.DayOfYear && hier.Year == report.endDateTime.Year)
                                            attach = false;
                                        switch (orderDays)
                                        {
                                            case 3:
                                            case 7:
                                            case 21: attach = false; break;
                                        }
                                        if (attached == 0) // We've already skipped the attachment once. It won't be extended anymore.
                                            attach = true;
                                    }
                                    if (System.Diagnostics.Debugger.IsAttached)
                                        Db.sendReport(process, "tcapiaccess@tc.tc", report.title + " " + attach.ToString(), report.body, outputFolder + filename + ".pdf");

                                    if (attach)
                                        Sfo.SFOBase.OrderAddAttachement(process, report.sfo_orderId, outputFolder + filename + ".pdf");
                                    else
                                        Db.execSqlCommand(process, "UPDATE sfo_orders set attached=0 WHERE sfo_orderId like \"" + report.sfo_orderId + "\"");
                                }
                                else
                                    Db.sendReport(process, report.emails, report.title, report.body, outputFolder + filename + ".pdf");

                                Db.setReportSuccess(process);
                            }
                            else
                                Db.setReportFailure(process, "PDF file not found");
                        }
                        #endregion
                        #endregion
                    }
                }
                catch (Exception ex)
                {
                    process.log(ex.Message);
                    process.log(ex.StackTrace);
                    Db.sendError(process, "Erreur de rapport " + process.report.reportQueueId + "/" + process.report.orderId);
                    Db.setReportFailure(process, ex.Message);
                }
                finally
                {
                    process.conn.Close();
                    if (dr != null)
                        if (dr.IsClosed == false)
                            dr.Close();
                }
            }
            MethodInvoker ldispose = delegate { process.listbox.Dispose(); };
            process.listbox.Invoke(ldispose);
            MethodInvoker pdispose = delegate { process.tabpage.Dispose(); };
            process.tabpage.Invoke(pdispose);
            clsGlobal.g_Processes.Remove(process.guid); 
        }

        #region formManagement
        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = false;

            string threadGroupString = (ConfigurationManager.AppSettings["threadsGroups"] != null) ? ConfigurationManager.AppSettings["threadsGroups"] : "";
            string[] threadGroups = threadGroupString.Split(',');

            int nbReportingThreads = (ConfigurationManager.AppSettings["NbReportingThreads"] != null) ? Convert.ToInt16(ConfigurationManager.AppSettings["NbReportingThreads"]) : 1;

            for (int i = 0; i < threadGroups.Length ; i++)
            {
                Process process = new Process();

                process.thread = new Thread(() => processing(process));
                process.group = Convert.ToInt16(threadGroups[i]);
                process.thread.IsBackground = true;
                if (i == 0 && System.Diagnostics.Debugger.IsAttached) Db.execSqlCommand(process, "UPDATE 0_scheduler set running = null where debug=1");

                process.tabpage = new TabPage();
                clsGlobal.g_VControl.tabControl1.TabPages.Add(process.tabpage);
                process.listbox = new ListBox();
                process.listbox.Dock = DockStyle.Fill;
                process.listbox.HorizontalScrollbar = true;
                process.listbox.ScrollAlwaysVisible = true;
                process.listbox.MouseDoubleClick += new MouseEventHandler(OnItemDoubleClick);
                process.tabpage.Controls.Add(process.listbox);
                process.guid = Guid.NewGuid();
                this.components.Add(process.listbox);
                clsGlobal.g_Processes.Add(process.guid, process);
                process.thread.Start();
                process.log("Starting " + process.guid.ToString());

                Thread.Sleep(2000);
            }

            for (int i = 0; i < nbReportingThreads; i++)
            {
                // Reporting process
                Process process = new Process();

                process.thread = new Thread(() => reporting(process));
                process.group = 99;
                process.thread.IsBackground = true;

                process.tabpage = new TabPage();
                clsGlobal.g_VControl.tabControl1.TabPages.Add(process.tabpage);
                process.listbox = new ListBox();
                process.listbox.Dock = DockStyle.Fill;
                process.listbox.HorizontalScrollbar = true;
                process.listbox.ScrollAlwaysVisible = true;
                process.listbox.MouseDoubleClick += new MouseEventHandler(OnItemDoubleClick);
                process.tabpage.Controls.Add(process.listbox);
                process.guid = Guid.NewGuid();
                this.components.Add(process.listbox);
                clsGlobal.g_Processes.Add(process.guid, process);
                process.thread.Start();
                process.log("Starting " + process.guid.ToString());

                Thread.Sleep(2000);
            }
        }
        public void OnItemDoubleClick(object sender, EventArgs e)
        {
            Clipboard.SetText(((ListBox)sender).Text);
        }
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            clsGlobal.g_Running = false;
            timer2.Enabled = true;
        }
        private void timer2_Tick(object sender, EventArgs e)
        {
            if (clsGlobal.g_Processes.Count == 0)
                Application.Exit();
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (e.CloseReason == CloseReason.WindowsShutDown) return;
            if (!closingMessageDisplayed && clsGlobal.g_Processes.Count > 0)
            {
                closingMessageDisplayed = true;
                if (MessageBox.Show(this, "Closing started, waiting for running tasks to complete?", "Closing", MessageBoxButtons.YesNo) == DialogResult.No)
                {
                    foreach (Process process in clsGlobal.g_Processes.Values)
                    {
                        clsGlobal.g_Processes.Remove(process.guid);
                        Db.setFailure(process, "Application ended");
                    }
                    Application.Exit();
                }
            }
            if (clsGlobal.g_Processes.Count > 0)
            {
                e.Cancel = true;
                clsGlobal.g_Running = false;
                timer2.Enabled = true;
            }
        }
        #endregion

        #region helpers
        static string formatDate(DateTime dt, string lang, XmlDocument xLanguage)
        {
            string ret = "";

            if (dt != null)
            {
                if (lang == "fr")
                    ret = dt.Day + " " + xLanguage.SelectSingleNode("/Language/Months/Month[position()=" + dt.Month + "]/@" + lang).Value + ", " + dt.Year;
                else
                    ret = xLanguage.SelectSingleNode("/Language/Months/Month[position()=" + dt.Month + "]/@" + lang).Value + " " + dt.Day + ", " + dt.Year;
            }

            return ret;
        }
        static string formatDayMonth(DateTime dt, string lang, XmlDocument xLanguage)
        {
            string ret = "";

            if (dt != null)
            {
                if (lang == "fr")
                    ret = dt.Day + " " + xLanguage.SelectSingleNode("/Language/Months/Month[position()=" + dt.Month + "]/@" + lang).Value;
                else
                    ret = xLanguage.SelectSingleNode("/Language/Months/Month[position()=" + dt.Month + "]/@" + lang).Value + " " + dt.Day;
            }

            return ret;
        }
        static string getPct(long arg, long div)
        {
            if (div == 0)
                return "0";

            return Convert.ToString(Math.Ceiling(Convert.ToDecimal(arg * 10000) / div) / 100);
        }
        static int ShellExec(String command)
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                UseShellExecute = false,
                LoadUserProfile = true,
                ErrorDialog = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8,

                FileName = "cmd.exe",
                Arguments = "/c " + command
            };

            System.Diagnostics.Process shell = new System.Diagnostics.Process();
            shell.StartInfo = info;
            shell.EnableRaisingEvents = true;

            shell.Start();
            shell.BeginErrorReadLine();
            shell.BeginOutputReadLine();
            shell.WaitForExit();

            return shell.ExitCode;
        }
        static bool renderIt(string xmlPath, string templatePath, string pdfPath)
        {
            bool success = false;
            try
            {
                if (File.Exists(pdfPath)) File.Delete(pdfPath);

                // XML string
                XmlDocument xDoc = new XmlDocument();
                xDoc.Load(xmlPath);
                String xmlString = xDoc.OuterXml; 

                // Convert XML string to a byte array
                byte[] xmlBytes = Encoding.UTF8.GetBytes(xmlString);

                using (Stream xmlStream = new MemoryStream(xmlBytes))
                {
                    using (Stream pdfStream = new FileStream(pdfPath, FileMode.Create, FileAccess.Write))
                    {
                        // Render the XML document
                        OutputInformation info = Render(xmlStream, templatePath, pdfStream);

                        Console.WriteLine("Document {0} rendered successfully. The document has {1} page(s).\n", pdfPath, info.PageCount);
                    }
                }

                success = true;
            }
            catch (Exception ex)
            {
                // Report errors
                Console.Out.WriteLine(ex.Message);
                if (ex.InnerException != null)
                    Console.Out.WriteLine(ex.InnerException.ToString());

                // Delete partial file
                if (File.Exists(pdfPath))
                    File.Delete(pdfPath);
            }
            return success;
        }
        static OutputInformation Render(Stream xmlStream, String templatePath, Stream pdfStream)
        {
            // Initialize input data source
            IDataSource inputData = new XmlDataSource(xmlStream, Engine.InputFormat.XML);

            // Declare a new instance of output information
            OutputInformation info = new OutputInformation();

            // Initialize rendering parameters
            RenderingParameters rp = new RenderingParameters();

            // Set output format
            rp.OutputFormat = Ecrion.Ultrascale.Engine.OutputFormat.PDF;

            // Set local template
            rp.Template = new LocalDocumentTemplate(templatePath);

            // Set base URL for resources with relative paths
            rp.BaseUrl = Path.GetDirectoryName(templatePath);

            // Initialize rendering engine
            Ecrion.Ultrascale.Engine engine = new Ecrion.Ultrascale.Engine();

            // Call the engine to render the document
            engine.Render(inputData, pdfStream, rp, ref info);

            return info;
        }
        #endregion
    }
}
