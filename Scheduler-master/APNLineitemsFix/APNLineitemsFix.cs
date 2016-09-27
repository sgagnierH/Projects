using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tc.TcMedia.Scheduler;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Tc.TcMedia.Apn;

namespace APNLineitemsFix
{
    public class APNLineitemsFix : iScheduler
    {
        public void Run(Process process)
        {
            SeatConfig config = APNBase.getSeats(process);

            foreach(Seat seat in config.seats)
            {
                if (seat.no == 1) break;

                seat.auth = new APNAuth(seat.username, seat.password);
                if (!seat.auth.authenticated) throw new Exception("Can't login to account no " + seat.no);

                MySqlConnection ownConnection = Db.getConnection();
                MySqlDataReader dr = null;

                try
                {
                    dr = Db.getMySqlReader(process, "SELECT lineitemId FROM apn_lineitems WHERE sfo_orderlineNo is not null and lifetime_budget_imps is null", ownConnection);
                    while (dr.Read())
                    {
                        process.log(dr.GetInt64("lineitemId").ToString());
                        dynamic response = APNBase.callApi(process, seat.auth, "line-item?id=" + dr.GetInt64("lineitemId"));
                        if (response.status.Value == "OK")
                            APNBase.checkApnObject(process, "apn_lineitems", "lineitemId", response["line-item"]);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    dr.Close();
                }
            }          
        }
    }
}
