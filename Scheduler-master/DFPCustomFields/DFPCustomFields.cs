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

namespace DFPCustomFields
{
    public class DFPCustomFields : iScheduler
    {
        public void Run(Process process) 
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            // Get the CustomFieldService.
            CustomFieldService customFieldService = (CustomFieldService)interfaceUser.GetService(DfpService.v201508.CustomFieldService);
            customFieldService.Timeout = 300000; // 5 min

            // Create a statement to get all custom fields.
            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id ASC")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            // Sets default for page.
            CustomFieldPage page = new CustomFieldPage();

            do
            {
                // Get custom fields by statement.
                Console.WriteLine(System.DateTime.Now.ToString(Db.TIME_FORMAT) + " Loading...");
                page = customFieldService.getCustomFieldsByStatement(statementBuilder.ToStatement());

                if (page.results != null)
                {
                    int i = page.startIndex;
                    foreach (CustomField customField in page.results)
                    {
                        Console.Write(++i + "/" + page.totalResultSetSize + " " + customField.name);
                        DFPBase.checkDfpObject(process, "dfp_customfields", "customFieldId", customField);
                    }
                }

                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            Console.WriteLine("\nNumber of results found: " + page.totalResultSetSize);

            Console.WriteLine("\nChecking missing values");

            MySqlDataReader reader = Db.getMySqlReader(process, "SELECT customFieldOptionId FROM dfp_customfields_v_missing");
            while(reader.Read())
            {
                long customFieldOptionId = reader.GetInt64("customFieldOptionId");
                CustomFieldOption customFieldOption = customFieldService.getCustomFieldOption(customFieldOptionId);
                Db.execSqlCommand(process, "INSERT INTO dfp_customfieldsvalues (customFieldId, customFieldOptionId, displayName) VALUES (" + customFieldOption.customFieldId + "," + customFieldOptionId + ",'" + customFieldOption.displayName + "')  ON DUPLICATE KEY UPDATE displayName='" + customFieldOption.displayName + "'");
            }
        }
    }
}
