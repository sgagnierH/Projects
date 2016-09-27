using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Tc.TcMedia.Scheduler;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

namespace GPTScripts
{
    public class GPTScripts : iScheduler
    {
        Process _process;

        public void Run(Process process)
        {
            MySqlDataReader reader;
            _process = process;
            _process.log("Checking new missing script domains");
            string repoRoot = System.Configuration.ConfigurationManager.AppSettings["gptRepo"];
            string domRoot = repoRoot + "pb\\dom\\";

            string customscript = null;
            string dom = null;
            string url = null;
            string adunit = null;
            string domori = null;
            long id = -1;

            bool found = true;
            while (found)
            {
                reader = Db.getMySqlReader(process, "SELECT * FROM gpt_scriptmissing_v");
                if (reader.Read())
                {
                    id = reader.GetInt64("missingId");
                    dom = reader.GetString("dom").Replace("%3A", ":").Replace("%2F", "/");
                    customscript = domRoot + dom.Substring(0, 1) + "\\" + dom + "\\custom.js";
                    url = reader.GetString("url");
                    adunit = reader.GetString("adunit").Split('/')[0];
                    domori = reader.GetString("dom");
                    if (url.Length > 8)
                    {
                        int pos = url.IndexOf('/', 8);
                        if (pos > -1) url = url.Substring(0, pos + 1);
                        _process.log(id + " : " + dom + ", " + adunit + ", " + url);
                    }
                    else
                    {
                        deleteMissing(domori);
                    }
                } 
                else
                {
                    found = false;
                }
                reader.Close();

                if (found)
                {
                    if (isIp(url))
                        deleteMissing(domori);
                    else if (domainExists(dom))
                        deleteMissing(domori);
                    else if (createDomain(dom, url, domori))
                    {
                        Db.execSqlCommand(_process, "INSERT INTO gpt_scriptmissing_done (dom, url, adunit) VALUES('" + dom + "','" + url + "','" + adunit + "')");
                        deleteMissing(domori);
                    }
                }
            }

            process.log("Checking for adUnits");
            // Find AdUnit Missing infos
            reader = Db.getMySqlReader(process, "SELECT domainId, adUnit FROM gpt_domains WHERE rejected=0 AND domainId NOT IN (SELECT domainId from gpt_domains_adunits)");
            while (reader.Read())
            {
                string adUnit = reader.GetString("adUnit");
                long domainId = reader.GetInt64("domainId");

                if (adUnit.IndexOf(".fr.") > 0)
                {
                    Db.execSqlCommand(_process, "INSERT INTO gpt_domains_adunits (domainId, adUnitId, adUnitCode_full, siteId, lang) SELECT " + domainId + ", dfp_adUnitId, dfp_adUnitCode_full, apn_siteId, 'fr' FROM lnk_dfp_apn WHERE dfp_adUnitCode_full='" + adUnit + "'");
                    Db.execSqlCommand(_process, "INSERT INTO gpt_domains_adunits (domainId, adUnitId, adUnitCode_full, siteId, lang) SELECT " + domainId + ", dfp_adUnitId, dfp_adUnitCode_full, apn_siteId, 'en' FROM lnk_dfp_apn WHERE dfp_adUnitCode_full='" + adUnit.Replace(".fr.", ".en.") + "'");
                }
                else if (adUnit.IndexOf(".en.") > 0)
                {
                    Db.execSqlCommand(_process, "INSERT INTO gpt_domains_adunits (domainId, adUnitId, adUnitCode_full, siteId, lang) SELECT " + domainId + ", dfp_adUnitId, dfp_adUnitCode_full, apn_siteId, 'en' FROM lnk_dfp_apn WHERE dfp_adUnitCode_full='" + adUnit + "'");
                    Db.execSqlCommand(_process, "INSERT INTO gpt_domains_adunits (domainId, adUnitId, adUnitCode_full, siteId, lang) SELECT " + domainId + ", dfp_adUnitId, dfp_adUnitCode_full, apn_siteId, 'fr' FROM lnk_dfp_apn WHERE dfp_adUnitCode_full='" + adUnit.Replace(".en.", ".fr.") + "'");
                }
                else
                {
                    Db.execSqlCommand(_process, "INSERT INTO gpt_domains_adunits (domainId, adUnitId, adUnitCode_full, siteId) SELECT " + domainId + ", dfp_adUnitId, dfp_adUnitCode_full, apn_siteId FROM lnk_dfp_apn WHERE dfp_adUnitCode_full='" + adUnit + "'");
                }
            }

            reader.Close();

            reader = Db.getMySqlReader(process, "SELECT count(*) AS nb FROM gpt_domains WHERE rebuild=1");
            if (reader.Read())
            {
                process.log(reader.GetInt64("nb") + " script(s) to rebuild");
                if(reader.GetInt64("nb") > 0)
                {
                    // Calling CircleCI api to process new domain files to build.
                    CI ci = JsonConvert.DeserializeObject<CI>(process.schedule.Config);
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://circleci.com/api/v1/project/" + ci.ci_username + "/" + ci.ci_project + "/tree/master?circle-token=" + ci.ci_token);
                    byte[] data = Encoding.ASCII.GetBytes("");
                    Stream requestStream = null;
                    request.Method = "POST";
                    request.Timeout = 30000;
                    request.ContentType = "application/x-www-form-urlencoded";
                    requestStream = request.GetRequestStream();
                    requestStream.Write(data, 0, data.Length);
                    requestStream.Close();

                    int success = 0;
                    int retries = 0;
                    while (success == 0 && retries < 5)
                    {
                        try
                        {
                            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                            if (response.StatusCode.ToString() == "Created")
                            {
                                success = 1;
                                process.log("Command sent to Circle CI");
                            }
                        }
                        catch (Exception ex)
                        {
                            retries++;

                            System.Threading.Thread.Sleep(6000); // Don't flood API
                            process.log("Retrying " + retries + " - " + ex.Message);
                            if (retries == 5)
                                throw new Exception("Unable to reach CircleCI's API", ex);
                        }
                    }
                }
            }
        }

        public void deleteMissing(string domain)
        {
            Db.execSqlCommand(_process, "DELETE FROM gpt_scriptmissing WHERE dom='" + domain + "'");
        }
        public bool isIp(string address)
        {
            bool ret;
            IPAddress addressO;
            string isItIp = address.Split('/')[2].Split(':')[0];
            if (IPAddress.TryParse(isItIp, out addressO))
                ret = true;
            else if (isItIp == "localhost")
                ret = true;
            else
                ret = false;
            return ret;
        }
        public bool domainExists(string domain)
        {
            bool exists = false;
            MySqlDataReader reader = Db.getMySqlReader(_process, "SELECT domainId from gpt_domains WHERE domain='" + domain + "'");
            if (reader.Read())
                exists = true;
            reader.Close();
            return exists;
        }
        public long getAdUnitid(string adUnit)
        {
            long adUnitId = -1;
            MySqlDataReader reader = Db.getMySqlReader(_process, "SELECT adUnitId from dfp_adunits_v_2nd WHERE adUnitCodeFull='" + adUnit + "'");
            if (reader.Read())
                adUnitId = reader.GetInt64("AdUnitId");
            reader.Close();
            return adUnitId;
        }
        public bool createDomain(string dom, string url, string adUnit)
        {
            bool ret = false;
            bool duplicateException = false;

            try
            {
                Db.execSqlCommand(_process, "INSERT INTO gpt_domains (domain, adUnit, url)  VALUES ('" + dom + "','" + adUnit + "','" + url + "')");
                ret = true;
            }
            catch(Exception ex)
            {
                duplicateException = false;
                if(ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    if(ex.GetType() == typeof(MySqlException))
                        if (((MySqlException)ex).ErrorCode == 1036)
                            duplicateException = true;
                }
                if (!duplicateException)
                {
                    throw new Exception("Problem processing missing domains", ex);
                }
            }

            return ret;
        }
    }
    public class CI
    {
        public string ci_username;
        public string ci_project;
        public string ci_token;
    }
}
