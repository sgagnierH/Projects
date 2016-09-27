using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;

namespace Tc.TcMedia.Apn
{
    public class APNAuth
    {
        public Cookie cookie = null;
        public string jsonResponse = null;
        public bool authenticated = false;
        public DateTime leased = new DateTime();
        public string username = "";
        private string _password = "";
        private const string authURL = "https://api.appnexus.com/auth";
        public const string apiUrl = "http://api.appnexus.com/";

        public APNAuth(string userName, string password)
        {
            login(userName, password);
        }

        private void login(string userName, string password)
        {
            string jsonAuth = "{ \"auth\": { \"username\" : \"" + userName + "\", \"password\" : \"" + password + "\" } }";
            byte[] data = Encoding.ASCII.GetBytes(jsonAuth);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(authURL);
            request.Method = "POST";
            request.CookieContainer = new CookieContainer();
            request.ContentType = "application/x-www-form-urlencoded";

            try
            {
                Stream requestStream = request.GetRequestStream();
                requestStream.Write(data, 0, data.Length);
                requestStream.Close();

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode.ToString() == "OK" && response.Cookies.Count > 0)
                {
                    cookie = response.Cookies[0];
                    username = userName;
                    _password = password;
                    authenticated = true;
                    leased = System.DateTime.Now;

                    Stream responseStream = response.GetResponseStream();
                    StreamReader myStreamReader = new StreamReader(responseStream, Encoding.Default);
                    jsonResponse = myStreamReader.ReadToEnd();

                    myStreamReader.Close();
                    responseStream.Close();
                }
                else
                {
                    throw new APNApiException("Can't login to AppNexus");
                }
                response.Close();
            }
            catch(Exception ex)
            {
                throw new APNApiException("Can't login to AppNexus", ex);
            }
        }

        public bool renew(){
            jsonResponse = null;
            authenticated = false;
            login(username, _password);
            return authenticated;
        }
    }
}
