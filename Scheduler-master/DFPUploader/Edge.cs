using System;
using System.Reflection;
using System.Threading;
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
    class Edge
    {
        public static string DealWithEdge(string zip, string dest)
        {
            object myInstance = DFPUploader.GetUpload();

            var insideDir = Directory.GetDirectories(dest)[0];
            var cleanDir = dest;
            var root = Path.GetFileNameWithoutExtension(cleanDir);

            DFPUploader.MapOverFileHref(DFPUploader.GetFiles(cleanDir), (string file, string path) => ChangeFile(file, path));
            
            var treahd = new Thread(new ThreadStart(() => DFPUploader.upload.Invoke(myInstance, new object[] { root, cleanDir, DFPUploader.DFPInfo })));
            treahd.Start();
            treahd.Join();
            return OpenFiles(cleanDir, root);
        }

        static private void ChangeFile(string file, string hrefPath)
        {
            var text = File.ReadAllText(file);

            text = DFPUploader.GetGetScript(text);
            text = text.Replace(@"'%%CLICK_URL_UNESC%%'", @"decodeURIComponent(getGet(""clickTracker""))");
            text = text.Replace(@"""%%CLICK_URL_UNESC%%""", @"decodeURIComponent(getGet(""clickTracker""))");
            text = text.Replace(@"'%%CLICK_URL_ESC%%'", @"decodeURIComponent(getGet(""clickTracker""))");
            text = text.Replace(@"""%%CLICK_URL_ESC%%""", @"decodeURIComponent(getGet(""clickTracker""))");
            text = text.Replace(@"'%%DEST_URL%%'", @"decodeURIComponent(getGet(""clickTracker""))");
            text = text.Replace(@"""%%DEST_URL%%""", @"decodeURIComponent(getGet(""clickTracker""))");
            File.WriteAllText(file, text);
        }

        static string OpenFiles(string path, string root)
        {
            string[] files = DFPUploader.GetFiles(path);
            string hrefPath = "";
            foreach (var f in files)
            {
                DirectoryInfo d = new DirectoryInfo(f);
                hrefPath = d.Parent.Parent.Name;
                if (d.Parent.Name == root)
                    return OpenFile(d.Name, root);
                else
                    return OpenFile(d.Parent.Name, root, folder: d.Parent.Name + "/");
            }
            return "";
        }

        static string OpenFile(string sizes, string path, string folder = "")
        {
            DirectoryInfo d = new DirectoryInfo(path);
            var size = d.Parent.Name;
            var text = DFPUploader.GetIframeScript(path, folder, sizes);
            return text;
        }
    }
}
