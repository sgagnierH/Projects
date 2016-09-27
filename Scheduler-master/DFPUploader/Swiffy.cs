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
    class Swiffy
    {
        public static string DealWithSwiffy(string zip, string dest)
        {
            object myInstance = DFPUploader.GetUpload();
            var file = DFPUploader.GetFiles(dest)[0];

            ChangeFile(file);
            var root = System.IO.Path.GetFileNameWithoutExtension(file);
            var ext = new DirectoryInfo(file).Extension;
            var parent = new DirectoryInfo(file).Parent.Name;
            root += ext;

            var r = new Regex("style=\"width: (.*)px; height: (.*)px");
            var res = r.Matches(File.ReadAllText(file));
            var size = res[0].Groups[1].Value + "x" + res[0].Groups[2].Value;

            var th = new Thread(new ThreadStart(() => DFPUploader.upload.Invoke(myInstance, new object[] { parent, dest, DFPUploader.DFPInfo })));
            th.Start();
            th.Join();
            return OpenFile(Path.GetFileNameWithoutExtension(dest), root, size);
        }

        static string OpenFile(string title, string file, string sizes)
        {
            return DFPUploader.GetIframeScript(title, file: file, clickUrl: "%%CLICK_URL_UNESC%%%%DEST_URL%%");
        }

        static private void ChangeFile(string file)
        {
            var text = File.ReadAllText(file);
            text = DFPUploader.GetGetScript(text);
            text = text.Replace(@"stage.start();", @"stage.setFlashVars(""clickTAG=""+decodeURIComponent(getGet('clickTracker')));
    stage.start();");
            text = text.Replace(@"swiffycontainer", @"swiffycontainer_%ecid!");
            text = text.Replace(@"</script>", @"
var tclink = document.querySelector('a');
if(tclink)
    tclink.href = decodeURIComponent(getGet('clickTracker'));
</script>");
            File.WriteAllText(file, text);
        }
    }
}
