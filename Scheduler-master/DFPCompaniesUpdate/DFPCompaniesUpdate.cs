using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;
using Google.Api.Ads.Common.Util;
using Tc.TcMedia.Scheduler;
using Tc.TcMedia.Dfp;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

namespace DFPCompaniesUpdate
{
    public class DFPCompaniesUpdate : iScheduler
    {
        public void Run(Process process)
        {
            Command cmd = JsonConvert.DeserializeObject<Command>(process.schedule.Config);

            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();
            StringBuilder sb = new StringBuilder();
            long companyId = 0;

            // Get the CompanyService.
            CompanyService companyService = (CompanyService)interfaceUser.GetService(DfpService.v201508.CompanyService);

            process.log("Getting companies with missing info");
            if (process.conn.State == System.Data.ConnectionState.Open) process.conn.Close();

            MySqlDataReader dataReader = Db.getMySqlReader(process, "SELECT * FROM dfp_companies_v_lastupdated_contact WHERE email is NULL");
            while (dataReader.Read())
            {
                string line = "";
                process.log("Processing Company " + dataReader.GetString("name"));
                StatementBuilder statementBuilder = new StatementBuilder()
                    .Where("id = :companyId")
                    .AddValue("companyId", dataReader.GetInt64("companyId"));

                try {
                    // Get the companies by statement.
                    CompanyPage page = companyService.getCompaniesByStatement(statementBuilder.ToStatement());

                    Company company = page.results[0];
                    companyId = company.id;
                    sb.Append(companyId + " - " + company.name + " : ");

                    // Update the company comment
                    if(company.primaryContactId == null)
                    {
                        try
                        {
                            company.primaryContactId = dataReader.GetInt64("contactId");
                            line += ((line == "") ? "" : "," ) + " primaryContact='" + dataReader.GetInt64("contactId") + "'";
                        }
                        catch(Exception){}
                    }

                    if(company.email == null)
                    {
                        try
                        {
                            company.email = dataReader.GetString("contactEmail");
                            line += ((line == "") ? "" : "," ) + " email='" + dataReader.GetString("contactEmail") + "'";
                        }
                        catch(Exception){}
                    }

                    if(company.address == null)
                    {
                        try
                        {
                            company.address = dataReader.GetString("contactAddress");
                            line += ((line == "") ? "" : "," ) + " address='" + dataReader.GetString("contactAddress").Replace("'","''") + "'";
                        }
                        catch(Exception){}
                    }

                    if(company.primaryPhone == null)
                    {
                        try
                        {
                            company.primaryPhone = dataReader.GetString("contactPhone");
                            line += ((line == "") ? "" : "," ) + " primaryPhone='" + dataReader.GetString("contactPhone") + "'";
                        }
                        catch(Exception){}
                    }

                    if(line != "")
                    {
                        company.comment = company.comment + "\nUpdated programmatically by Sylvain Gagnier on " + System.DateTime.Now.ToString();
                        company.comment = company.comment + "\n" + line.Replace("''","'");
                        Db.execSqlCommand(process, "UPDATE dfp_companies SET " + line + " WHERE companyId=" + companyId);

                        // Update the company on the server.
                        Company[] companies = companyService.updateCompanies(new Company[] {company});
                        sb.Append(line);
                    }
                } 
                catch (Exception ex) 
                {
                    throw new Exception("Problem reading database for companyId=" + companyId, ex);
                }
                sb.AppendLine("");
            }

            process.log("Reporting");
            Db.sendMail(process, cmd.toEmail, cmd.title, sb.ToString());
        }
    }
    public class Command
    {
        public string title { get; set; }
        public string toEmail { get; set; }
        
        public Command()
        {
            title = "Updates in DFP Companies contact info";
            toEmail = "tcapiaccess@tc.tc,amy.lamoreaux@tc.tc";
        }
    }
}
