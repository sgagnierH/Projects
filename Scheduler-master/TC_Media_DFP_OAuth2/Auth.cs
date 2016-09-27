using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;

namespace Tc.TcMedia.Dfp
{
    public class Auth
    {
        public static DfpUser getDfpUser()
        {
            DfpUser dfpUser;

            dfpUser = new DfpUser();
            dfpUser.OAuthProvider.Config.OAuth2ClientId = "608398656828-o975kcl9qpihkqudp7h4j06pi6ig1mqh.apps.googleusercontent.com";
            dfpUser.OAuthProvider.Config.OAuth2ClientSecret = "G0GdwKWPkE-ypsNgwZw4nxQJ";
            dfpUser.OAuthProvider.Config.OAuth2RefreshToken = "1/MouafQyasFP6OVLZADf7Zu3YBGcQyrsb6hqH53c-djY"; // tcapiaccess@tc.tc
            //"1/zlIttSJWF_mGwiXIvNYoKsTzi3KqWQTBytJP2tFl-Os"; sylvain.gagnier@tc.tc


            return dfpUser;
        }
    }
}
