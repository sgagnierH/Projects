using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tc.TcMedia.Scheduler;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Tc.TcMedia.Apn;

namespace APNPlacements
{
    public class APNPlacements : iScheduler
    {
        public void Run(Process process)
        {
            SeatConfig config = APNBase.getSeats(process);

            foreach(Seat seat in config.seats)
            {
                seat.auth = new APNAuth(seat.username, seat.password);
                if (!seat.auth.authenticated) throw new Exception("Can't login to account no " + seat.no);

                seat.placementIds = new Dictionary<long, long>();

                string SQL = "Select distinct placementId from apn_report_placements where seat = " + seat.no + " and placementId NOT IN (select placementId from apn_placements where seat=" + seat.no + ")";
                process.log("Getting" + ((process.inDaySchedule) ? " missing" : "") + " APN Placements for seat " + seat.no);

                MySqlDataReader dataReader = Db.getMySqlReader(process, SQL);
                try
                {
                    if (!dataReader.HasRows)
                        process.log("Nothing to do");

                    while (dataReader.Read())
                    {
                        long placementId = dataReader.GetInt64("placementId");
                        seat.placementIds.Add(placementId, placementId);
                    }
                    process.log("Chargement terminé");
                }
                catch (Exception ex)
                {
                    Db.sendError(process, ex.Message);
                    throw new Exception("Error getting ids from MySql", ex);
                }
                finally
                {
                    dataReader.Close();
                }

                int i = 0;
                foreach (long placementId in seat.placementIds.Keys)
                {
                    process.log(++i + "/" + seat.placementIds.Count + " seat : " + seat.no + " - " + placementId);
                    dataReader = Db.getMySqlReader(process, "Select * from apn_placements where placementId = " + placementId + " and seat = " + seat.no);
                    bool added = false;
                    dynamic placement = null;

                    if (!dataReader.Read())
                    {
                        Db.execSqlCommand(process, "INSERT into apn_placements (placementId, seat) VALUES (" + placementId + "," + seat.no + ")");
                        Db.execSqlCommand(process, "INSERT INTO 0_logs (ObjectName,ObjectValue,Info,Data) VALUES('placementId'," + placementId + ",'Add','Seat = " + seat.no + "')");
                        process.log(" ... New");
                        dataReader.Close();

                        // Refresh
                        dataReader = Db.getMySqlReader(process, "Select * from apn_placements where placementId = " + placementId + " and seat = " + seat.no);
                        dataReader.Read();
                        added = true;
                    }
                    if (!dataReader.GetBoolean("no_data"))
                    {
                        placement = APNBase.getPlacementById(process, seat.auth, placementId);
                        if (placement == null)
                        {
                            Db.execSqlCommand(process, "UPDATE apn_placements SET no_data=1 WHERE placementId=" + placementId + " AND seat= " + seat.no);
                        }
                        else
                            APNBase.processCheck(process, placement, dataReader, "apn_placements", "placementId", placementId, added);
                    }
                    dataReader.Close();
                }
            }          
        }
    }
}
