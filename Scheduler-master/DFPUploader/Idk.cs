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
    class Idk
    {
        public static void DealWithIt(string zip, string dest)
        {
            object myInstance = DFPUploader.GetUpload();

            var insideDir = Directory.GetDirectories(dest)[0];
            var cleanDir = dest;
            var root = System.IO.Path.GetFileNameWithoutExtension(cleanDir);

            DFPUploader.MapOverFileHref(DFPUploader.GetFiles(insideDir), (string file, string path) => ChangeFile(file, path));
            DFPUploader.MapOverFileHref(DFPUploader.GetFiles(insideDir, "*.js"), (string file, string path) => ChangeJS(file, path));

            //var lib = new DirectoryInfo(Directory.GetDirectories(insideDir, "libs")[0]).FullName;

            new Thread(new ThreadStart(() => DFPUploader.upload.Invoke(myInstance, new object[] { root, cleanDir, DFPUploader.DFPInfo }))).Start();
            new Thread(new ThreadStart(() => OpenFiles(insideDir, root))).Start();
            //new Thread(new ThreadStart(() => DFPUploader.upload.Invoke(myInstance, new object[] { "libs", lib, DFPUploader.DFPInfo }))).Start();
        }
        
        static private void ChangeJS(string file, string hrefPath)
        {
            var text = File.ReadAllText(file);
            text = text.Replace(@"src:""images", @"src:""" + @"http:" + DFPUploader.DFPInfo["baseHref"].Replace(@"%%FOLDER%%", hrefPath) + "images");

            File.WriteAllText(file, text);
        }

        static private void ChangeFile(string file, string hrefPath)
        {
            DirectoryInfo d = new DirectoryInfo(hrefPath);
            var size = d.Name;
            var jsre = new Regex(@"(" + size + @")\.js");
            var text = File.ReadAllText(file);
            var linkres = jsre.Match(text);

            text = DFPUploader.GetGetScript(text);
            text = text.Replace(@"libs/", @"http:" + DFPUploader.DFPInfo["baseHref"].Replace(@"%%FOLDER%%", "libs"));
            text = text.Replace(@"http://code.createjs.com/", @"http:" + DFPUploader.DFPInfo["baseHref"].Replace(@"%%FOLDER%%", "libs"));
            text = text.Replace(@"src:""images", @"src:""" + @"http:" + DFPUploader.DFPInfo["baseHref"].Replace(@"%%FOLDER%%", hrefPath) + "images");
            text = text.Replace(@"'%%CLICK_URL_UNESC%%'", @"decodeURIComponent(getGet(""clickTracker""))");
            text = text.Replace(@"blank"">", @"blank""><script>document.getElementsByTagName(""a"")[0].href = decodeURIComponent(getGet('clickTracker'));</script>");
            text = text.Replace(@"style=""", @"style=""margin:0px;");
            text = jsre.Replace(text, @"http:" + DFPUploader.DFPInfo["baseHref"].Replace(@"%%FOLDER%%", hrefPath) + size + ".js");
            File.WriteAllText(file, text);
        }

        static void OpenFiles(string path, string root)
        {
            string[] files = DFPUploader.GetFiles(path);
            string hrefPath = "";
            foreach (var f in files)
            {
                DirectoryInfo d = new DirectoryInfo(f);
                hrefPath = d.Parent.Parent.Name;
                OpenFile(d.Parent.Name, root);
            }
        }

        static void OpenFile(string sizes, string path)
        {
            DirectoryInfo d = new DirectoryInfo(path);
            // La taille du creatif est le nom du dossier parent au fichier index.html
            var size = d.Parent.Name;
            var text = DFPUploader.GetIframeScript(path, "%%WIDTH%%x%%HEIGHT%%/", "%%WIDTH%%x%%HEIGHT%%");
            if (!String.Equals(DFPUploader.fileout, ""))
            {

                DFPUploader.WriteJSON();
                return;
            }
        }
    }
}
