using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.hearst.db;
using com.hearst.dfp;
using com.hearst.utils;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201605;
using Google.Api.Ads.Dfp.v201605;
using Google.Api.Ads.Common.Util;
using MySql.Data.MySqlClient;

namespace com.hearst.dfp
{
    class dfpCustomFields : com.hearst.dfp.dfpBase
    {
        static List<long> orders = new List<long>();

        static void Main(string[] args)
        {
            dbBase.log("Starting");

            dfpCustomFields run = new dfpCustomFields();
            run.getPages(run, "dfp_customfields", new Google.Api.Ads.Dfp.v201605.CustomFieldPage(), "customfieldId", args);

            // Check for missing values
            DfpUser dfpUser = com.hearst.utils.OAuth2.getDfpUser();
            CustomFieldService customFieldService = run.getService(dfpUser);
            MySqlDataReader reader = dbBase.getSqlReader("SELECT customFieldOptionId FROM dfp_customfields_v_missing order by customFieldOptionId");
            while (reader.Read())
            {
                long customFieldOptionId = reader.GetInt64("customFieldOptionId");
                CustomFieldOption customFieldOption = customFieldService.getCustomFieldOption(customFieldOptionId);
                MySqlConnection conn2 = dbBase.getNewConnection();
                dbBase.log(customFieldOption.customFieldId + "," + customFieldOptionId + ",'" + customFieldOption.displayName);
                dbBase.execSqlCommand("INSERT INTO dfp_customfieldsvalues (customFieldId, customFieldOptionId, displayName) VALUES (" + customFieldOption.customFieldId + "," + customFieldOptionId + ",'" + customFieldOption.displayName + "')  ON DUPLICATE KEY UPDATE displayName='" + customFieldOption.displayName + "'", conn2);
                conn2.Close();
                conn2.Dispose();
            }
            Console.ReadKey();
        }
        public override dynamic getService(DfpUser user)
        {
            return user.GetService(DfpService.v201605.CustomFieldService);
        }
        public override dynamic getPage(dynamic service, StatementBuilder statementBuilder, int offset = StatementBuilder.SUGGESTED_PAGE_LIMIT)
        {
            statementBuilder = new StatementBuilder()
                .OrderBy("id ASC")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            return service.getCustomFieldsByStatement(statementBuilder.ToStatement());
        }
        public override void processCustomField(dynamic item, dynamic property, dynamic dataReader, ref string strSql, ref bool fieldProcessed, ref int i)
        {
            //string fieldName = dataReader.GetName(i);
            //dynamic value = (property == null) ? null : property.GetValue(item, null);
            //CustomField customField = (CustomField)item;
        }
    }
}
