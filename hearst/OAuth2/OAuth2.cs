using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Api.Ads.Dfp.Lib;

namespace com.hearst.utils
{
    public class OAuth2
    {
        public static DfpUser getDfpUser()
        {
            DfpUser dfpUser;

            dfpUser = new DfpUser();
            // sgagnier@hearst.com
            dfpUser.OAuthProvider.Config.OAuth2ClientId = "425314150275-u0b5hb863j9191irefmqsrmotct6u7lk.apps.googleusercontent.com";
            dfpUser.OAuthProvider.Config.OAuth2ClientSecret = "Tb0UYWXoVA0T7cx3BzhLIPIQ";
            dfpUser.OAuthProvider.Config.OAuth2RefreshToken = "1/9futU1qtLPc1pEb880u2hcva4meJDirD6osnENy0QrM";

            return dfpUser;
        }
    }
}
