using System;
using System.Reflection;
using System.Threading;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using WinSCP;
using System.Management;
using System.Diagnostics;

namespace DFPUploader
{
    class Flexitive
    {
        public static string DealWithFlex(string zip, string dest)
        {
            object myInstance = DFPUploader.GetUpload();

            var insideDir = Directory.GetDirectories(dest)[0];
            Regex reg = new Regex(@"\..*\..*\.zip$");
            var cleanDir = new DirectoryInfo(zip).Parent.FullName + "\\" + DateTime.Now.GetTimestamp();
            var root = Path.GetFileNameWithoutExtension(cleanDir);


            try
            {
                Directory.Move(insideDir, cleanDir);
            }
            catch (Exception ex)
            {
                DFPUploader.errstr += ex.Message + "\n";
                DFPUploader.WriteErr();
                Directory.Delete(dest, true);
                System.Environment.Exit(-1);
            }

            DFPUploader.MapOverFileHref(DFPUploader.GetFiles(cleanDir), (string file, string path) => ChangeFile(file, path));

            var thread = new Thread(new ThreadStart(() => DFPUploader.upload.Invoke(myInstance, new object[] { root, cleanDir, DFPUploader.DFPInfo })));
            thread.Start();
            thread.Join();
            return OpenFiles(cleanDir);
        }

        static private void ChangeFile(string file, string hrefPath)
        {
            var linkRegexp = new Regex(@"link"":{""url"":""(.*?)""}");
            var imgRegexp = new Regex(@"(\.\/)");
            var text = File.ReadAllText(file);
            var linkres = linkRegexp.Match(text);

            text = imgRegexp.Replace(text, DFPUploader.DFPInfo["baseHref"].Replace(@"%%FOLDER%%", hrefPath));
            text = DFPUploader.GetGetScript(text);
            text = text.Replace(@"%%DEST_URL%%", linkres.Groups[1].Value);
            text = text.Replace(@"%%TARGET_WINDOW%%", "_blank");
            text = text.Replace(@"'%%CLICK_URL_UNESC%%'", @"decodeURIComponent(getGet(""clickTracker""))");
            text = text.Replace(@"'%%CLICK_URL_ESC%%'", @"decodeURIComponent(getGet(""clickTracker""))");
            File.WriteAllText(file, text);
        }

        static string OpenFiles(string path)
        {
            string[] files = DFPUploader.GetFiles(path);
            string s = "";
            string prefix = "";
            string hrefPath = "";
            foreach (var f in files)
            {
                DirectoryInfo d = new DirectoryInfo(f);
                hrefPath = d.Parent.Parent.Name;
                s += d.Parent.ToString().Split('_').Last() + ",";
                prefix = String.Join("_", d.Parent.ToString().Split('_').Take(d.Parent.ToString().Split('_').Length - 1)) + "_";
            }
            if (prefix == "_")
                prefix = "";
            return OpenFile(s, hrefPath, prefix);
        }

        static string OpenFile(string sizes, string path, string prefix)
        {
            DirectoryInfo d = new DirectoryInfo(path);
            var size = d.Parent.Name;
            var text = ""; 
            foreach (var s in sizes.Split(','))
            {
                if (s == "")
                    continue;
                if(s == "responsive")
                    text = DFPUploader.GetIframeScript(path, prefix + "responsive/");
                else
                    text = DFPUploader.GetIframeScript(path, prefix + "%%WIDTH%%x%%HEIGHT%%/");
                DFPUploader.multipleSnippet.Add(s, text);
            }
            return text;
        }
    }
}
