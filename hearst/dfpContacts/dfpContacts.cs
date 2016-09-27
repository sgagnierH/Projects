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

namespace com.hearst.dfp
{
    class dfpContacts : com.hearst.dfp.dfpBase
    {
        static List<long> orders = new List<long>();

        static void Main(string[] args)
        {
            dbBase.log("Starting");

            dfpContacts run = new dfpContacts();
            run.getPages(run, "dfp_contacts", new Google.Api.Ads.Dfp.v201605.CompanyPage(), "contactId", args);

            Console.ReadKey();
        }
        public override dynamic getService(DfpUser user)
        {
            return user.GetService(DfpService.v201605.ContactService);
        }
        public override dynamic getPage(dynamic service, StatementBuilder statementBuilder, int offset = StatementBuilder.SUGGESTED_PAGE_LIMIT)
        {
            statementBuilder = new StatementBuilder()
                .OrderBy("id ASC")
                .Limit(offset);

            return service.getContactsByStatement(statementBuilder.ToStatement());
        }
        public override void processCustomField(dynamic item, dynamic property, dynamic dataReader, ref string strSql, ref bool fieldProcessed, ref int i)
        {
            //string fieldName = dataReader.GetName(i);
            //dynamic value = (property == null) ? null : property.GetValue(item, null);
            //Contact contact = (Contact)item;
        }
    }
}
