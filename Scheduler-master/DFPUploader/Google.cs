using System;
using System.Reflection;
using System.Threading;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using WinSCP;

namespace DFPUploader
{
    class Google
    {
        public static string DealWithGWD(string zip, string dest)
        {
            object myInstance = DFPUploader.GetUpload();

            var insideDir = dest;
            var cleanDir = dest;
            var root = Path.GetFileNameWithoutExtension(cleanDir);

            DFPUploader.MapOverFileHref(DFPUploader.GetFiles(cleanDir), (string file, string path) => ChangeFile(file, path));

            var thread = new Thread(new ThreadStart(() => DFPUploader.upload.Invoke(myInstance, new object[] { root, cleanDir, DFPUploader.DFPInfo })));
            thread.Start();
            thread.Join();
            return OpenFiles(cleanDir, root);
        }

        static private void ChangeFile(string file, string hrefPath)
        {
            var text = File.ReadAllText(file);

            text = DFPUploader.GetGetScript(text, "<head>");
            File.WriteAllText(file, text);
        }

        static string OpenFiles(string path, string root)
        {            
            var file = DFPUploader.GetFiles(path)[0];
            DirectoryInfo d = new DirectoryInfo(file);
            return DFPUploader.GetIframeScript(root, file: d.Name, clickTracker: "clickTAG", clickUrl: "%%CLICK_URL_UNESC%%%%DEST_URL%%");
        }
    }
}
