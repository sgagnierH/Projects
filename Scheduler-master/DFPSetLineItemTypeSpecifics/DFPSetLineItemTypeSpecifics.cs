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

namespace DFPSetLineItemTypeSpecifics
{
    public class DFPSetLineItemTypeSpecifics : iScheduler
    {
        public void Run(Process process)
        {
            Command cmd = JsonConvert.DeserializeObject<Command>(process.schedule.Config);
            //<option>-- select --</option>
            //<option value="26685">Display ROC</option>
            //<option value="26805">Display RON</option>
            //<option value="26565">Display ROS</option>
            //<option value="26925">Newsletters</option>
            //<option value="27045">NSBD</option>
            //<option value="29085">Remnants</option>
            //<option value="28965">Resold</option>
            //<option value="27165">To specify</option>

            DfpUser user = Tc.TcMedia.Dfp.Auth.getDfpUser();

            StringBuilder sb = new StringBuilder();

            Dictionary<long, LineItem> lineItems = new Dictionary<long, LineItem>();
            LineItemService lineItemService = (LineItemService)user.GetService(DfpService.v201508.LineItemService);
            LineItemPage lineItemPage = new LineItemPage();

            StatementBuilder lineItemStatementBuilder = new StatementBuilder()
                .OrderBy("id ASC")
                .Where("id in (" + cmd.lineitemIds + ")")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            sb.AppendLine("orderId,lineItem.id,customFieldId,customfieldValue");

            bool found;
            try
            {
                do
                {
                    lineItemPage = lineItemService.getLineItemsByStatement(lineItemStatementBuilder.ToStatement());
                    if (lineItemPage.results != null && lineItemPage.results.Length > 0)
                    {
                        foreach (LineItem lineItem in lineItemPage.results)
                        {
                            if (lineItem.isArchived)
                            {
                                sb.AppendLine(lineItem.orderId + "," + lineItem.id + "," + cmd.customFieldId + ",Archived - not modified");
                            } else {
                                found = false;
                                List<BaseCustomFieldValue> customFieldValues = new List<BaseCustomFieldValue>();

                                // Transferring actuel custom fields
                                if (lineItem.customFieldValues != null)
                                {
                                    for (int i = 0; i < lineItem.customFieldValues.Length; i++)
                                    {
                                        process.log(lineItem.orderId + " - " + lineItem.id + " " + lineItem.customFieldValues[i].customFieldId);
                                        if (lineItem.customFieldValues[i].customFieldId == cmd.customFieldId)
                                        {
                                            found = true;
                                            // If we found the field, we replace the old value with the new one.
                                            ((DropDownCustomFieldValue)lineItem.customFieldValues[i]).customFieldOptionId = cmd.customValueId;
                                        } 
                                        customFieldValues.Add(lineItem.customFieldValues[i]);
                                    }
                                }

                                if (!found)
                                {
                                    // Adding the new one
                                    DropDownCustomFieldValue customFieldValue = new DropDownCustomFieldValue();
                                    customFieldValue.customFieldId = cmd.customFieldId;
                                    customFieldValue.customFieldOptionId = cmd.customValueId;
                                    customFieldValues.Add(customFieldValue);
                                }

                                lineItem.customFieldValues = customFieldValues.ToArray();
                                lineItems.Clear();
                                lineItems.Add(lineItem.id, lineItem);

                                try
                                {
                                    lineItemService.updateLineItems(lineItems.Values.ToArray());
                                    sb.AppendLine(lineItem.orderId + "," + lineItem.id + "," + cmd.customFieldId + "," + cmd.customValueId);
                                }
                                catch(Exception ex)
                                {
                                    sb.AppendLine(lineItem.orderId + "," + lineItem.id + "," + cmd.customFieldId + "," + cmd.customValueId + " ERREUR: " + ex.Message);
                                    process.log(ex.Message);
                                }
                            }
                        }
                    }
                    lineItemStatementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
                } while (lineItemStatementBuilder.GetOffset() < lineItemPage.totalResultSetSize);
            }
            catch (Exception e)
            {
                Console.WriteLine("Can't get linetems for " + cmd.lineitemIds + ". Exception says " + e.Message);
            }
            string filename = "C:\\temp\\lineItemTypes" + process.guid + ".res";
            System.IO.File.WriteAllText(filename, sb.ToString());

            if (cmd.emails == null) cmd.emails = "tcapiaccess@tc.tc";
            Db.sendMail(process, cmd.emails, "Requested LineItem Type Update", "Here's the status of your request", filename);
        }
    }
    class Command
    {
        public long customFieldId { get; set; }
        public long customValueId { get; set; }
        public string lineitemIds { get; set; }
        public string emails { get; set; }
    }
}
