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
    class dfpPlacements : com.hearst.dfp.dfpBase
    {
        static List<long> orders = new List<long>();

        static void Main(string[] args)
        {
            dbBase.log("Starting");

            dfpPlacements run = new dfpPlacements();
            run.getPages(run, "dfp_placements", new Google.Api.Ads.Dfp.v201605.PlacementPage(), "placementId", args);

            Console.ReadKey();
        }
        public override void runCustomCode(dynamic item)
        {
            Placement placement = (Placement)item;
            Guid guid = new Guid();
            MySqlConnection conn2 = dbBase.getNewConnection();
            dbBase.execSqlCommand("UPDATE dfp_placements_adunits SET working='" + guid + "' WHERE placementId=" + placement.id, conn2);
            if (placement.targetedAdUnitIds != null)
                foreach (string adunitId in placement.targetedAdUnitIds)
                    dbBase.execSqlCommand("INSERT INTO dfp_placements_adunits (placementId, adunitId) values(" + placement.id + "," + adunitId + ") ON DUPLICATE KEY UPDATE working=null", conn2);

            dbBase.execSqlCommand("DELETE FROM dfp_placements_adunits WHERE working='" + guid + "' AND placementId=" + placement.id, conn2);
            conn2.Close();
            conn2.Dispose();
        }
        public override dynamic getService(DfpUser user)
        {
            return user.GetService(DfpService.v201605.PlacementService);
        }
        public override dynamic getPage(dynamic service, StatementBuilder statementBuilder, int offset = StatementBuilder.SUGGESTED_PAGE_LIMIT)
        {
            return service.getPlacementsByStatement(statementBuilder.ToStatement());
        }
        public override void processCustomField(dynamic item, dynamic property, dynamic dataReader, ref string strSql, ref bool fieldProcessed, ref int i)
        {
            //string fieldName = dataReader.GetName(i);
            //dynamic value = (property == null) ? null : property.GetValue(item, null);
            //Placement placement = (Placement)item;
        }
    }
}
