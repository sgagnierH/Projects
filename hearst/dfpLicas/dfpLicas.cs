using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.hearst.db;
using com.hearst.dfp;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201605;
using Google.Api.Ads.Dfp.v201605;
using Google.Api.Ads.Common.Util;
using MySql.Data.MySqlClient;

namespace com.hearst.dfp
{
    class dfpLicas : com.hearst.dfp.dfpBase
    {
        static List<long> orders = new List<long>();

        static void Main(string[] args)
        {
            dbBase.log("Starting");

            dfpLicas run = new dfpLicas();
            getPagesCustom(run, "dfp_licas", new Google.Api.Ads.Dfp.v201605.LineItemCreativeAssociationPage(), "licaId", args);

            Console.ReadKey();
        }
        public static void getPagesCustom(dfpBase runner, string table, dynamic pageType, string keyField, string[] args, int offset = StatementBuilder.SUGGESTED_PAGE_LIMIT)
        {
            string lastModifiedDateTime = null;
            if (args.Length > 0)
                if (args[0].ToLower() == "all")
                    lastModifiedDateTime = "";

            if (lastModifiedDateTime == null)
                lastModifiedDateTime = dbBase.getLastModifiedDateTime(table);

            dbBase.log("Getting items" + ((lastModifiedDateTime == "") ? "" : " since " + lastModifiedDateTime));

            dynamic service = runner.getService(dfpUser);

            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("lastModifiedDateTime ASC")
                .Where((lastModifiedDateTime == "") ? "" : "lastModifiedDateTime > :lastModifiedDateTime")
                .AddValue("lastModifiedDateTime", lastModifiedDateTime)
                .Limit(offset);

            int i = 0;
            dynamic page = null;

            do
            {
                int success = 0;
                int retries = 0;
                while (success == 0 && retries < 5)
                {
                    try
                    {
                        dbBase.log("Loading...");
                        page = runner.getPage(service, statementBuilder, offset);
                        dbBase.log(" .. Loaded ");
                        success = 1;
                    }
                    catch (Exception ex)
                    {
                        retries++;
                        if (retries == 5)
                        {
                            dbBase.log("Retry " + retries);
                            dbBase.sendError(ex.Message);
                            throw new Exception("Unable to process");
                        }
                    }
                }

                if (page.results != null && page.results.Length > 0)
                {
                    foreach (dynamic item in page.results)
                    {
                        dbBase.log(i++ + "/" + page.totalResultSetSize);
                        checkLineItemCreativeAssociation(runner, table, keyField, item);
                    }
                }

                statementBuilder.IncreaseOffsetBy(offset);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            dbBase.log("Number of items found: " + page.totalResultSetSize);
        }
        public static void checkLineItemCreativeAssociation(dfpBase runner, string tableName, string keyfield, LineItemCreativeAssociation obj)
        {
            MySqlDataReader dataReader = dbBase.getSqlReader("Select * from dfp_licas where creativeId = " + obj.creativeId + " and lineitemId = " + obj.lineItemId);
            if (dataReader.Read())
                dfpBase.processCheck(runner, obj, dataReader, "dfp_licas", "licaId", dataReader.GetInt64("licaId"));
            else
            {
                StringBuilder param = new StringBuilder();
                StringBuilder values = new StringBuilder();

                param.Append("creativeId"); values.Append(obj.creativeId);
                param.Append(",creativeSetId"); values.Append("," + obj.creativeSetId);
                param.Append(",lineitemId"); values.Append("," + obj.lineItemId);
                param.Append(",lastModifiedDateTime"); values.Append(",'" + dfpBase.toSqlDate(obj.lastModifiedDateTime) + "'");

                dbBase.log(" ... New");

                MySqlConnection conn2 = dbBase.getNewConnection();
                dbBase.execSqlCommand("INSERT into dfp_licas (" + param.ToString() + ") VALUES (" + values.ToString() + ")", conn2);
                conn2.Close();
                conn2.Dispose();
            }

            dataReader.Close();
        }
        public override dynamic getService(DfpUser user)
        {
            return user.GetService(DfpService.v201605.LineItemCreativeAssociationService);
        }
        public override dynamic getPage(dynamic service, StatementBuilder statementBuilder, int offset = StatementBuilder.SUGGESTED_PAGE_LIMIT)
        {
            return service.getLineItemCreativeAssociationsByStatement(statementBuilder.ToStatement());
        }
        public override void processCustomField(dynamic item, dynamic property, dynamic dataReader, ref string strSql, ref bool fieldProcessed, ref int i)
        {
            //string fieldName = dataReader.GetName(i);
            //dynamic value = (property == null) ? null : property.GetValue(item, null);
            //LineItemCreativeAssociation lica = (LineItemCreativeAssociation)item;
        }
    }
}
