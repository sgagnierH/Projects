using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DFPUploader
{
    public class Info
    {
        public static string url = @"conversys.upload.akamai.com";
        public static int port = 22;
        public static string user = "sshacs";
        public static string passwd = @"s3cr3t";
        public static string ppkPath = @"private.ppk";
        public static string baseDir = "/401497/flex/";
        public static string baseHref = @"//cdn.tcadops.ca/flex/%%FOLDER%%/";

        public Dictionary<String, String> getInfo()
        {
            var dic = new Dictionary<String, String>();
            dic.Add("url", url);
            dic.Add("port", port.ToString());
            dic.Add("user", user);
            dic.Add("passwd", passwd);
            dic.Add("ppkPath", ppkPath);
            dic.Add("baseDir", baseDir);
            dic.Add("baseHref", baseHref);
            return dic;
        }
    }
}
