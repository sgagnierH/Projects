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
    class dfpCreativeWrappers : com.hearst.dfp.dfpBase
    {
        static List<long> orders = new List<long>();

        static void Main(string[] args)
        {
            dbBase.log("Starting");

            dfpCreativeWrappers run = new dfpCreativeWrappers();
            run.getPages(run, "dfp_creativewrappers", new Google.Api.Ads.Dfp.v201605.CreativeWrapperPage(), "creativewrapperId", args);

            Console.ReadKey();
        }
        public override dynamic getService(DfpUser user)
        {
            return user.GetService(DfpService.v201605.CreativeWrapperService);
        }
        public override dynamic getPage(dynamic service, StatementBuilder statementBuilder, int offset = StatementBuilder.SUGGESTED_PAGE_LIMIT)
        {
            // Overwrite defaule statementBiolder
            statementBuilder = new StatementBuilder()
                .OrderBy("id ASC")
                .Limit(offset);

            return service.getCreativeWrappersByStatement(statementBuilder.ToStatement());
        }
        public override void processCustomField(dynamic item, dynamic property, dynamic dataReader, ref string strSql, ref bool fieldProcessed, ref int i)
        {
            string fieldName = dataReader.GetName(i);
            dynamic value = (property == null) ? null : property.GetValue(item, null);
            CreativeWrapper creativeWrapper = (CreativeWrapper)item;

            #region htmlSnippet
            if (!fieldProcessed && fieldName == "header_htmlSnippet")
            {
                if (dataReader[fieldName].ToString() != creativeWrapper.header.htmlSnippet.Replace("'", "''"))
                    strSql += ",header_htmlSnippet='" + creativeWrapper.header.htmlSnippet.Replace("'", "''") + "'";

                fieldProcessed = true;
            }

            if (!fieldProcessed && fieldName == "footer_htmlSnippet")
            {
                if (dataReader[fieldName].ToString() != creativeWrapper.footer.htmlSnippet.Replace("'", "''"))
                    strSql += ",footer_htmlSnippet='" + creativeWrapper.footer.htmlSnippet.Replace("'", "''") + "'";

                fieldProcessed = true;
            }
            #endregion
        }
    }
}
