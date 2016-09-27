using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.hearst.db;
using com.hearst.dfp;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201605;
using Google.Api.Ads.Dfp.v201605;
using Google.Api.Ads.Common.Util;
using System.Xml.Serialization;

namespace com.hearst.dfp
{
    class dfpOrders : com.hearst.dfp.dfpBase
    {
        static List<long> orders = new List<long>();

        static void Main(string[] args)
        {
            dbBase.log("Starting");

            dfpOrders run = new dfpOrders();
            run.getPages(run, "dfp_orders", new Google.Api.Ads.Dfp.v201605.OrderPage(), "orderId", args);

            Console.ReadKey();
        }
        public override dynamic getService(DfpUser user)
        {
            return user.GetService(DfpService.v201605.OrderService);
        }
        public override dynamic getPage(dynamic service, StatementBuilder statementBuilder, int offset = StatementBuilder.SUGGESTED_PAGE_LIMIT)
        {
            return service.getOrdersByStatement(statementBuilder.ToStatement());
        }
        public override void processCustomField(dynamic item, dynamic property, dynamic dataReader, ref string strSql, ref bool fieldProcessed, ref int i)
        {
            string fieldName = dataReader.GetName(i);
            dynamic value = (property == null) ? null : property.GetValue(item, null);
            Order creatorderive = (Order)item;
        }
        public override dynamic getDeserialized(string xmlString)
        {
            Order order = new Order();
            try
            {
                XmlSerializer x = new XmlSerializer(typeof(Order));
                order = (Order)x.Deserialize(new StringReader(xmlString.Replace("&apos;", "'")));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return order;
        }
    }
}
