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
    class dfpCreatives : com.hearst.dfp.dfpBase
    {
        static List<long> orders = new List<long>();

        static void Main(string[] args)
        {
            dbBase.log("Starting");
            Dictionary<long, string> advertisers = new Dictionary<long, string>();
            #region header bidders
            advertisers.Add(46328242, "HNP | NYC | A9 (XXXXXX)");
            advertisers.Add(47574562, "HNP | NYC | INDEX EXCHANGE (FKA CASALE) (XXXXXX)");
            advertisers.Add(53212282, "HNP | NYC | THE RUBICON PROJECT (XXXXXX)");
            advertisers.Add(53204482, "HNP | NYC | OPENX (XXXXXX)");
            advertisers.Add(85442122, "HNP | NYC | Yieldbot (XXXXXX)");
            advertisers.Add(53058082, "HNP | NYC | Sonobi (XXXXXX)");
            #endregion
            #region remnant
            advertisers.Add(149321242, "HNP | NYC | Bidfluence (XXXXXX)");
            advertisers.Add(99465802, "HNP | NYC | Facebook (XXXXXX)");
            advertisers.Add(85446202, "HNP | NYC | Genesis Media (XXXXXX)");
            advertisers.Add(53211082, "HNP | NYC | GOOGLE ADX (XXXXXX)");
            advertisers.Add(83426242, "HNP | NYC | Google Partner Select (XXXXXX)");
            advertisers.Add(47576002, "HNP | NYC | KARGO (XXXXXX)");
            advertisers.Add(46780042, "HNP | NYC | MEDIA.NET (XXXXXX)");
            advertisers.Add(144768322, "HNP | NYC | NetSeer (XXXXXX)");
            advertisers.Add(55181242, "HNP | NYC | Teads (XXXXXX)");
            #endregion
            dfpBase.getOrdersFromAdvertisers(advertisers.Keys.ToList(), ref orders);

            dfpCreatives run = new dfpCreatives();
            run.getPages(run, "dfp_creatives", new Google.Api.Ads.Dfp.v201605.CreativePage(), "creativeId", args);

            Console.ReadKey();
        }
        public override dynamic getService(DfpUser user)
        {
            return user.GetService(DfpService.v201605.CreativeService);
        }
        public override dynamic getPage(dynamic service, StatementBuilder statementBuilder, int offset = StatementBuilder.SUGGESTED_PAGE_LIMIT)
        {
            return service.getCreativesByStatement(statementBuilder.ToStatement());
        }
        public override void processCustomField(dynamic item, dynamic property, dynamic dataReader, ref string strSql, ref bool fieldProcessed, ref int i)
        {
            string fieldName = dataReader.GetName(i);
            dynamic value = (property == null) ? null : property.GetValue(item, null);
            Creative creative = (Creative)item;

            #region creativeType
            if (!fieldProcessed && fieldName == "creativeType")
            {
                if (dataReader["creativeType"].ToString() != creative.GetType().Name)
                    strSql += ",creativeType='" + creative.GetType().Name + "'";
                fieldProcessed = true;
            }
            #endregion
            #region policyViolations
            if (!fieldProcessed && fieldName == "policyViolations" && creative.policyViolations != null)
            {
                string policyViolations = "";
                foreach (CreativePolicyViolation violation in creative.policyViolations)
                {
                    policyViolations += ((policyViolations == "") ? "" : ",") + violation.ToString();
                }
                if (dataReader["policyViolations"].ToString() != policyViolations)
                {
                    strSql += ",policyViolations='" + policyViolations + "'";

                    if (creative.GetType().Name == "CustomCreative" || creative.GetType().Name == "ThirdPartyCreative")
                    {
                        dbBase.log("Duplicating policyViolation detected creative");
                        dfpBase.createDuplicateCreative(creative.id, orders);
                    }
                }
                fieldProcessed = true;
            }
            #endregion
        }
    }
}
