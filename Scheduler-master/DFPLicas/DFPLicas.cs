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

namespace DFPLicas
{
    public class DFPLicas : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            string lastModifiedDateTime = Db.toMySqlDate(new System.DateTime(2000,1,1));
            if (process.inDaySchedule) lastModifiedDateTime = Db.getLastModifiedDateTime(process, "dfp_licas", 1);
            process.log("Getting all Licas" + ((lastModifiedDateTime == "") ? "" : " since " + lastModifiedDateTime));

            LineItemCreativeAssociationService licaService = (LineItemCreativeAssociationService)interfaceUser.GetService(DfpService.v201508.LineItemCreativeAssociationService);
            licaService.Timeout = Db.getTimeout();

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("creativeId DESC")
                .Where((lastModifiedDateTime == "") ? "" : "lastModifiedDateTime > :lastModifiedDateTime ")
                .AddValue("lastModifiedDateTime", lastModifiedDateTime)
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            LineItemCreativeAssociationPage page = new LineItemCreativeAssociationPage();
            do
            {
                int success = 0;
                int retries = 0;
                while (success == 0 && retries < 5)
                {
                    try
                    {
                        process.log("Loading...");
                        page = licaService.getLineItemCreativeAssociationsByStatement(statementBuilder.ToStatement());
                        process.log(" .. Loaded ");
                        success = 1;
                    }
                    catch (Exception ex)
                    {
                        retries++;
                        if (retries == 5)
                        {
                            process.log("Retry " + retries);
                            Db.sendError(process, ex.Message);
                        }
                    }
                }

                if (page.results != null && page.results.Length > 0)
                {
                    int i = page.startIndex;
                    foreach (LineItemCreativeAssociation lica in page.results)
                    {
                        process.log(++i + "/" + page.totalResultSetSize + " " + lica.creativeId + "/" + lica.lineItemId);
                        checkLica(process, lica);
                    }
                }
                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            process.log("Number found: " + page.totalResultSetSize);
        }

        public void checkLica(Process process, LineItemCreativeAssociation lica)
        {
            MySqlDataReader dataReader = Db.getMySqlReader(process, "Select * from dfp_licas where creativeId = " + lica.creativeId + " and lineitemId = " + lica.lineItemId);
            if (dataReader.Read())
                DFPBase.processCheck(process, lica, dataReader, "dfp_licas", "licaId", dataReader.GetInt64("licaId"));
            else
                addCreative(process, lica);

            dataReader.Close();
        }

        private void addCreative(Process process, LineItemCreativeAssociation lica)
        {
            StringBuilder param = new StringBuilder();
            StringBuilder values = new StringBuilder();

            param.Append("creativeId"); values.Append(lica.creativeId);
            param.Append(",creativeSetId"); values.Append("," + lica.creativeSetId);
            param.Append(",lineitemId"); values.Append("," + lica.lineItemId);
            param.Append(",lastModifiedDateTime"); values.Append(",'" + DFPBase.toMySqlDate(lica.lastModifiedDateTime) + "'");

            process.log(" ... New");
            Db.execSqlCommand(process, "INSERT into dfp_licas (" + param.ToString() + ") VALUES (" + values.ToString() + ")");
            Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValue,Info,Data) VALUES('lica'," + lica.creativeId + ",'Add','')");
        }
    }
}
