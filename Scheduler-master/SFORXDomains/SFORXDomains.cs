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
using Tc.TcMedia.SfoRx;
using SFORXBase.SFDC;

namespace SFORXDomains
{
    public class SFORXDomains : iScheduler
    {
        Dictionary<string, long>Templates = new Dictionary<string,long>();
        Dictionary<long, long>OrderIds = new Dictionary<long, long>();
        string missingDomains = "";

        public void Run(Process process)
        {
            // Authenticate to Redux's Saleforce
            Tc.TcMedia.SfoRx.SFORXBase.Authenticate(process);

            // For secondary datareader
            MySqlConnection conn2 = Db.getConnection();
            MySqlDataReader dr2 = null;

            try
            {
                // Get all domains with no AppNexus partners defined
                MySqlDataReader dr = Db.getMySqlReader(process, "select domainId, domain from gpt_domains where headerbidding=1 and rejected=0 and domainId not in (select domainId from gpt_domains_partners where partnerid=1)");
                while (dr.Read())
                {
                    long domainId = dr.GetInt64("domainId");
                    string domain = dr.GetString("domain");

                    // Get info from Salesforce
                    QueryResult qr = Tc.TcMedia.SfoRx.SFORXBase.getSOQL(process, "SELECT Placement_ID__c, Site__r.Id, Site__r.Active__c, Site__r.Language__c FROM HB_Placement__c WHERE Platform__c='AppNexus' AND Site__r.Domains__c='" + domain + "'");
                    if (qr.records != null)
                    {
                        /// Can have multiple languages
                        for (int i = 0; i < qr.records.Count(); i++)
                        {
                            HB_Placement__c rec = (HB_Placement__c)qr.records[i];
                            long AppNexusPlacementId = (long)rec.Placement_ID__c;
                            string lang = rec.Site__r.Language__c.Substring(0, 2).ToLower();
                            string SiteId = rec.Site__r.Id;
                            bool SiteActive = (bool)rec.Site__r.Active__c;
                            string adUnitId = "";

                            process.log(domainId + " - " + domain + " - " + lang);

                            QueryResult qrs = null;
                            qrs = Tc.TcMedia.SfoRx.SFORXBase.getSOQL(process, "SELECT Id, Name, Active__c, ID1__c FROM SiteAlias__c WHERE Associated_Site__r.Id='" + rec.Site__r.Id + "' AND Platform__C='DFP'");
                            if (qrs.records != null)
                            {
                                string adUnitsId = "";
                                for (int j = 0; j < qrs.records.Count(); j++)
                                {
                                    SiteAlias__c rec2 = (SiteAlias__c)qrs.records[j];
                                    // Setting adUnit Language in database
                                    Db.execSqlCommand(process, "UPDATE dfp_adunits SET lang='" + lang + "' WHERE adUnitId=" + rec2.ID1__c);

                                    // Getting all adUnits from DFP linked to this site
                                    adUnitsId += ((adUnitsId == "") ? "" : ",") + rec2.ID1__c;
                                    if (j == 0)
                                        adUnitId = rec2.ID1__c; // Default value
                                }

                                if (qrs.records.Count() > 1)
                                {
                                    // Getting the adUnitId that is linked to AppNexus
                                    dr2 = Db.getMySqlReader(process, "SELECT dfp_adUnitId FROM dfp_adunits_v_apn_placements WHERE dfp_adUnitId in (" + adUnitsId + ")", conn2);
                                    if (dr2.Read())
                                        adUnitId = dr2.GetInt64("dfp_adUnitId").ToString();
                                    dr2.Close();
                                }

                                // set domain / adunit link
                                dr2 = Db.getMySqlReader(process, "SELECT * from gpt_domains_adunits WHERE domainId=" + domainId + " AND adUnitId=" + adUnitId, conn2);
                                if(!dr2.HasRows)
                                    Db.execSqlCommand(process, "INSERT INTO gpt_domains_adunits (domainId, adUnitId, adUnitCode_full, lang) SELECT " + domainId + ", adUnitId, adUnitCode_full, '" + lang + "' FROM gpt_domains_v_lnk WHERE adUnitId=" + adUnitId + " ON DUPLICATE KEY UPDATE siteId=siteId");
                                dr2.Close();

                                // insert partner info
                                dr2 = Db.getMySqlReader(process, "SELECT * FROM gpt_domains_partners WHERE domainId=" + domainId + " AND partnerId=1", conn2);
                                if(!dr2.HasRows)
                                    Db.execSqlCommand(process, "INSERT INTO gpt_domains_partners (domainId, partnerId, parameters, enabled) VALUES (" + domainId + ",1," + AppNexusPlacementId + ",1)");
                                dr2.Close();
                            }
                        }
                    }
                    else
                    {
                        // This domain does not have AppNexus HB Placement in Salesforce
                        missingDomains += ((missingDomains == "") ? "" : "\n") + domain;
                    }
                }
                dr.Close();
            }
            catch(Exception e)
            {
                process.log(e.Message);
            }
            if(missingDomains != "")
            {
                Db.sendError(process, "Domains with missing PlacementId in SalesForce : \n" + missingDomains);
            }
            #region old
            /*
            try
            {
                QueryResult qr = Tc.TcMedia.SfoRx.SFORXBase.getSOQL(process, "SELECT Id, Name, Placement_ID__c, Platform__c, Site__r.Id, Site__r.Active__c, Site__r.Domains__c, Site__r.Language__c, Site__r.Name FROM HB_Placement__c ");
                long nbRecords = 0;
                while (qr.records != null)
                {
                    for (int i = 0; i < qr.records.Count(); i++)
                    {
                        HB_Placement__c rec = (HB_Placement__c)qr.records[i];
                        QueryResult qrs = null;
                        qrs = Tc.TcMedia.SfoRx.SFORXBase.getSOQL(process, "SELECT Id, Name, Active__c, ID1__c FROM SiteAlias__c WHERE Associated_Site__r.Id='" + rec.Site__r.Id + "' AND Platform__C='DFP'");
                        if (qrs.records != null)
                        {
                            for (int j = 0; j < qrs.records.Count(); j++)
                            {
                                SiteAlias__c rec2 = (SiteAlias__c)qrs.records[j];
                                string sql = "INSERT INTO gpt_sfxrx (HB_Placement_Id,HB_Placement_Name, HB_Placement_Placement_ID, HB_Placement_Platform, Site_Id, Site_Active, Site_Domains, Site_Language, Site_Name, SiteAlias_Id, SiteAlias_Active, SiteAlias_ID1) VALUES (";
                                sql += "'" + rec.Id + "','" + rec.Name + "','" + rec.Placement_ID__c + "','" + rec.Platform__c + "','" + rec.Site__r.Id + "'," + (((bool)rec.Site__r.Active__c) ? 1 : 0) + ",'" + rec.Site__r.Domains__c + "','" + rec.Site__r.Language__c + "','" + rec.Site__r.Name + "','" + rec2.Id + "'," + (((bool)rec2.Active__c) ? 1 : 0) + ",'" + rec2.ID1__c + "')";
                                try
                                {
                                    process.log(sql);
                                    Db.execSqlCommand(process, sql);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e.Message);
                                }
                                nbRecords++;
                            }
                        }
                    }
                }
            } 
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
            */
            #endregion
        }
    }
}
