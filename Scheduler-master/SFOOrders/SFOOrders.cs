using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tc.TcMedia.Scheduler;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System.Data.SqlClient;
using Tc.TcMedia.Sfo;

namespace SFOOrders
{
    public class SFOOrders : iScheduler
    {
        Dictionary<string, long>Templates = new Dictionary<string,long>();
        Dictionary<long, long>OrderIds = new Dictionary<long, long>();

        public void Run(Process process)
        {
            string lastModifiedDateTime = Db.getLastModifiedDateTime(process, "sfo_orders") + "Z";
            Tc.TcMedia.Sfo.SFOBase.getOrderInfo(process, null, lastModifiedDateTime); 
        }
    }
}
