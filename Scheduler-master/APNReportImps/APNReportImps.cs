using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tc.TcMedia.Scheduler;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Tc.TcMedia.Apn;
using System.IO;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;

namespace APNReportImps
{
    public class APNReportImps : iScheduler
    {
        public void Run(Process process)
        {
            SeatConfig config = APNBase.getSeats(process);
            Command cmd = JsonConvert.DeserializeObject<Command>(process.schedule.Config);
            if(cmd == null)
            {
                cmd = new Command();
                cmd.updateDfp = 0;
            }
            
            #region history
            try
            {
                process.log("Moving actual data to History");
                Db.execSqlCommand(process, "INSERT INTO apn_report_placements_history SELECT * FROM apn_report_placements");
            }
            catch(Exception)
            {
                process.log("Error copying to history");
            }
            Db.execSqlCommand(process, "TRUNCATE apn_report_placements");
            #endregion

            #region Appnexus report
            System.DateTime end = System.DateTime.UtcNow.AddHours(-2); // Reporting data is about 2 hours old.
            foreach (Seat seat in config.seats)
            {
                seat.auth = new APNAuth(seat.username, seat.password);

                string reportEnd = end.ToString("yyyy-MM-dd HH:mm:ss");
                string reportStart = end.AddHours(-24).ToString("yyyy-MM-dd HH:mm:ss");

                if (!seat.auth.authenticated) throw new Exception("Can't login to account no " + seat.no);
                string data = "{\n	\"report\" : {\n		\"report_type\" : \"network_analytics\",\n		\"columns\" : [\n			\"placement_id\",\n			\"placement_name\",\n			\"revenue\",\n			\"imps\"\n		],\n		\"start_date\" : \"" + reportStart + "\",		\"end_date\" : \"" + reportEnd + "\",\n		\"group\" : \"placement_id\",\n		\"format\" : \"csv\"\n	}\n}";
                dynamic report = APNBase.callApi(process, seat.auth, "report", data);

                string filePath = "c:\\temp\\" + end.ToString("yyyyMMddhhmmss") + "_" + seat.no + "_NetworkAnalytics.csv";
                if (report == null) throw new Exception("No report from Appnexus");

                string reportId = report.report_id;
                process.log("Report Id for Seat no " + seat.no + ": " + reportId + " ");
                APNBase.waitForReport(process, seat.auth, reportId, filePath);

                process.log("Cleaning temp table");
                Db.execSqlCommand(process, "TRUNCATE _tmp_apn_report_placements");

                process.log("inserting data in temp table");
                try
                {
                    MySqlBulkLoader bl = new MySqlBulkLoader(process.conn);
                    bl.TableName = "_tmp_apn_report_placements";
                    bl.FieldTerminator = ",";
                    bl.LineTerminator = "\n";
                    bl.FileName = filePath;
                    bl.FieldQuotationCharacter = '"';
                    bl.FieldQuotationOptional = true;
                    bl.NumberOfLinesToSkip = 1;
                    int inserted = bl.Load();

                    process.log("Appending actual data : " + inserted + " lines for seat " + seat.no);
                    Db.execSqlCommand(process, "INSERT INTO apn_report_placements (date, seat, placementId, revenue, impressions, average_cpm) SELECT '" + Db.SystemDateTimeToMySqlDate(end) + "'," + seat.no + ",placement_id, revenue, impressions, FLOOR(revenue*100000/impressions)*10000 FROM _tmp_apn_report_placements");
                }
                catch (Exception ex)
                {
                    process.log(ex.Message);
                    throw new Exception("Error adding file", ex);
                }
                finally
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
            }
            #endregion

            #region DFP Update
            if (cmd.updateDfp == 1)
            {
                DfpUser apiUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

                LineItemService lineItemService = (LineItemService)apiUser.GetService(DfpService.v201508.LineItemService);
                Dictionary<long, LineItem> lineItems = new Dictionary<long, LineItem>();

                MySqlDataReader dataReader;
                string SQL;

                long i = 0;
                long nbitems = 0;
                long transactionId = 0;
                string txDate = Db.toMySqlDate(end);

                SQL = "Insert into 0_transaction (guid, dateTime) VALUES ('" + process.guid.ToString() + "','" + txDate + "')";
                Db.execSqlCommand(process, SQL);

                SQL = "SELECT transactionId FROM 0_transaction WHERE guid='" + process.guid.ToString() + "' and dateTime='" + txDate + "'";
                dataReader = Db.getMySqlReader(process, SQL);
                dataReader.Read();
                transactionId = dataReader.GetInt64("transactionId");
                dataReader.Close();

                SQL = "Select COUNT(*) AS nb FROM apn_v_update_price where appnexusId is not NULL and average_cpm > 0 and impressions > 200";
                dataReader = Db.getMySqlReader(process, SQL);
                dataReader.Read();
                nbitems = dataReader.GetInt64("nb");
                dataReader.Close();

                SQL = "Select lineitemId, placementId, publisher_id, impressions, average_cpm, costPerUnit FROM apn_v_update_price where appnexusId is not NULL and average_cpm > 0 and impressions > 200 ORDER BY lineitemid desc, impressions desc ";
                dataReader = Db.getMySqlReader(process, SQL);
                try
                {
                    long lastlineitemId = 0;
                    while (dataReader.Read())
                    {
                        long lineitemId = dataReader.GetInt64("lineitemId");
                        long placementId = dataReader.GetInt64("placementId");
                        long publisherId = dataReader.GetInt64("publisher_id");
                        long impressions = dataReader.GetInt64("impressions");
                        string pref = " " + ++i + "/" + nbitems + " : " + lineitemId + " - ";

                        if (lineitemId == lastlineitemId) // On liste les modificatoins par lineitem et impressions desc. Si on a un même lineitem avec 2 seat, celui ave cle plus d'impressions sera traité
                        {
                            process.log(pref + "Skipping impressions:" + impressions);
                        }
                        else
                        {
                            lastlineitemId = lineitemId;
                            long average_cpm = dataReader.GetInt64("average_cpm");
                            long costPerUnit = dataReader.GetInt64("costPerUnit");
                            long pctchange1 = (costPerUnit == 0) ? 1000 : (100 * average_cpm) / costPerUnit;
                            long pctchange2 = (average_cpm == 0) ? 1000 : (100 * costPerUnit) / average_cpm;
                            long pctchange = Math.Abs(pctchange1 - pctchange2);

                            if (pctchange < 10)
                            {
                                process.log(pref + "Skipping impressions:" + impressions + ", %change:" + pctchange);
                            }
                            else
                            {   // Create a Statement to get the line item.
                                StatementBuilder statementBuilder = new StatementBuilder()
                                    .OrderBy("id DESC")
                                    .Where("id = :id")
                                    .AddValue("id", lineitemId)
                                    .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

                                // Sets default for page.
                                LineItemPage page = new LineItemPage();
                                page = lineItemService.getLineItemsByStatement(statementBuilder.ToStatement());

                                if (page.results != null && page.results.Length > 0)
                                {
                                    LineItem lineItem = page.results[0];
                                    lineItem.costPerUnit.microAmount = average_cpm;
                                    lineItem.lastModifiedByApp = "APNReportImps";
                                    if (!lineItems.ContainsKey(lineitemId))
                                        lineItems.Add(lineitemId, lineItem);
                                    else
                                    {
                                        if (lineItem.costPerUnit.microAmount < average_cpm)
                                            lineItems[lineitemId] = lineItem;
                                    }

                                    process.log(pref + "Updating impressions:" + impressions + ", %change:" + pctchange + ", placementId:" + placementId + ", publisherId:" + publisherId + ", average_cpm:" + average_cpm / 10000 + ", costPerUnit:" + costPerUnit / 10000);
                                    Db.execSqlCommand(process, "INSERT INTO 0_dfp_costs_update (dateTime, transactionId, lineitemId, placementId, publisherId, average_cpm, costPerUnit) VALUES ('" + txDate + "'," + transactionId + "," + lineitemId + "," + placementId + "," + publisherId + "," + average_cpm + "," + costPerUnit + ")");
                                }
                                else
                                {
                                    // lineitem nout found on DFP???
                                    throw new Exception("LineItem " + lineitemId + " not found on DFP");
                                }
                            }
                        }
                    }

                    process.log("Updating DFP : " + lineItems.Count + " items");
                    lineItemService.updateLineItems(lineItems.Values.ToArray());
                    Db.execSqlCommand(process, "DELETE FROM 0_transaction WHERE transactionid=" + transactionId);
                }
                catch (Exception ex)
                {
                    Db.sendError(process, ex.Message.Replace("'", "''"));
                    Db.execSqlCommand(process, "DELETE FROM 0_transaction WHERE transactionid=" + transactionId);
                    Db.execSqlCommand(process, "DELETE FROM 0_dfp_costs_update WHERE transactionid=" + transactionId);
                    throw new Exception("Error updating DFP", ex);
                }
                dataReader.Close();
            }
            #endregion
        }
    }
    public class placementreport
    {
        public long placementId { get; set; }
        public float revenue { get; set; }
        public long impresssions { get; set; }
    }
    public class Command
    {
        public int updateDfp { get; set; }
    }
}
