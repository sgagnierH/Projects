using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tc.TcMedia.Scheduler;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Tc.TcMedia.Apn;

namespace APNSites
{
    public class APNSites : iScheduler
    {
        public void Run(Process process)
        {
            SeatConfig config = APNBase.getSeats(process);

            foreach(Seat seat in config.seats)
            {
                seat.auth = new APNAuth(seat.username, seat.password);
                if (!seat.auth.authenticated) throw new Exception("Can't login to account no " + seat.no);

                int startnum = 0;
                int nbelem = 100;
                int retnb = 0;
                do
                {
                    retnb = 0;
                    dynamic response = APNBase.callApi(process, seat.auth, "site?num_elements=" + nbelem + "&start_element=" + startnum);
                    foreach (dynamic site in response["sites"])
                    {
                        retnb++;
                        process.log(startnum++ + " - " + (string)site.name);
                        APNBase.checkApnObject(process, "apn_sites", "site_id", site);
                    }
                    startnum += nbelem;
                } while (retnb > 0);
            }          
        }
    }
}
