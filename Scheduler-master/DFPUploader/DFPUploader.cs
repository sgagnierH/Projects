using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.IO;
using System.IO.Compression;
//using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Management;
using System.Diagnostics;
using Tc.TcMedia.Scheduler;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;
using Google.Api.Ads.Common.Util;
using Newtonsoft.Json;

namespace DFPUploader
{
    public static class MyExtensions
    {
        public static String GetTimestamp(this System.DateTime value)
        {
            return value.ToString("yyyyMMddHHmmssfff");
        }

        public static String ToSaneEncoding(this String str)
        {
            return Encoding.UTF8.GetString(Encoding.Convert(Encoding.UTF8, Encoding.GetEncoding("ISO-8859-1"), Encoding.UTF8.GetBytes(str)));
        }

        public static String ToUTF8(this String str)
        {
            return Encoding.UTF8.GetString(Encoding.Convert(Encoding.UTF8, Encoding.GetEncoding("UTF16"), Encoding.UTF8.GetBytes(str)));
        }
    }

    public class ArrayToString
    {
        public Dictionary<String, String> arr { get; set; }

        public ArrayToString()
        {
            arr = new Dictionary<String, String>();
        }

        public void Add(String key, String val)
        {
            var v = val.Replace('"', '\'').Replace(System.Environment.NewLine, @"\n").Replace("\t", " ");
            arr.Add(key, v);
        }

        public Boolean isEmpty()
        {
            return this.arr.Count == 0;
        }

        override public String ToString()
        {
            var res = "[";
            foreach (KeyValuePair<String, String> e in this.arr)
            {
                //res += "[" + e.Key + "," + e.Value + "],";
                res += "[" + "\"" + e.Key + "\",\"" + e.Value.Replace('"', '\'').Replace(System.Environment.NewLine, @"\n").Replace("\t", " ") + "\"" + "],";
            }
            return res.Substring(0, res.Length - 1) + "]";
        }
    }

    public class DFPUploader
    {
        private static string upDll = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\AkamiUpload.dll";
        private static string infoDll = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\DFPInfo.dll";
        public static Dictionary<String, String> DFPInfo = GetInfo();
        public static Dictionary<String, String> jsonOutput = new Dictionary<String, String>();
        public static TextWriter fileout = null;
        public static string errstr = "";
        public static MethodInfo upload;
        public static ArrayToString multipleSnippet = new ArrayToString();
        public static DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();
        public static CreativeService creativeService = (CreativeService)interfaceUser.GetService(DfpService.v201508.CreativeService);
        public static LineItemCreativeAssociationService licaService = (LineItemCreativeAssociationService)interfaceUser.GetService(DfpService.v201508.LineItemCreativeAssociationService);
        private static Boolean ONDFP = true;

        public enum TYPE
        {
            FLEX = 0,
            SWIFFY = 1,
            EDGE = 2,
            GWD = 3, // Google Web Designer
            WHOKNOWS = 4,
        }

        public static void DealWithAllTheCrap(object sender, UnhandledExceptionEventArgs e)
        {
            errstr += "There was an unexpected error.\n" + e.ExceptionObject.ToString();
            WriteErr();
            Environment.Exit(-1);
        }

        public static void DeleteWriteExit(string dest = "")
        {
            try
            {
                if (dest != "")
                    Directory.Delete(dest, true);
            }
            catch (Exception)
            {
                ;
            }
            WriteErr();
            Environment.Exit(-1);
        }

        public static void Main(string[] args)
        {
            string zip;
            string dest;
            string creativeName;
            string[] creativeSize = null;
            string snippet = "";
            long lineItemId = 0;
            long advertiserId = 0;
            string dest_url = "";

            creativeService.Timeout = Db.getTimeout();

            DFPInfo = GetInfo();

            System.AppDomain.CurrentDomain.UnhandledException += DealWithAllTheCrap;

            zip = args[0];//.ToSaneEncoding();
            creativeName = args[1].ToSaneEncoding();
            if (creativeName == "FINIT DFP FINIT DFP NON NON NEIN")
                ONDFP = false;
            else
            {
                creativeSize = args[2].Split('x');
                lineItemId = long.Parse(args[3]);
                advertiserId = long.Parse(args[4]);
                dest_url = args[5];
            }
            #region Nope
            if (!zip.EndsWith(".zip"))
            {
                errstr += "You need to pass a zip file.\n";
                DeleteWriteExit();
                return;
            }

            dest = GetDest(zip);
            ExtractZip(zip, dest);
            try
            {
                var zips = GetFiles(dest, "*.zip", SearchOption.TopDirectoryOnly);
                if (zips.Length > 0)
                {
                    #region ToImplementMoreThanOneCreative
                    errstr += "The file contains more than one creative, this isn't ready here yet.";
                    DeleteWriteExit(dest);
                    #endregion
                    foreach (var z in zips)
                    {
                        var d = GetDest(z);
                        ExtractZip(z, d);
                        ProcessFolder(z, d);
                    }
                }
                else
                {
                    snippet = ProcessFolder(zip, dest);
                    if (snippet == "")
                    {
                        errstr += "The snippet is empty";
                        DeleteWriteExit(dest);
                        throw new Exception("The snippet is empty.");
                    }
                }
            }
            catch (Exception e)
            {
                errstr += "The file isn't in a format that is supported here. Please refer to the Studio team.\nErr message: " + e.Message;
                DeleteWriteExit(dest);
            }
            #endregion
            if (ONDFP)
            {
                if (multipleSnippet.isEmpty())
                {
                    try
                    {
                        CreateDFPCreative(creativeName, creativeSize, lineItemId, advertiserId, snippet, dest_url);
                    }
                    catch (Exception e)
                    {
                        errstr += "DFP seems to be causing trouble.\n";
                        errstr += e.Message;
                        DeleteWriteExit(dest);
                    }
                }
                else
                {
                    // jsonOutput.Add("multiple", multipleSnippet.ToString());
                    foreach (KeyValuePair<String, String> e in multipleSnippet.arr)
                    {
                        CreateDFPCreative(creativeName + " " + e.Key, e.Key.Split('x'), lineItemId, advertiserId, e.Value, dest_url);
                    }
                }
            }
            else
            {
                if (multipleSnippet.isEmpty())
                {
                    multipleSnippet.Add("theone", snippet);

                }
                jsonOutput.Add("multiple", multipleSnippet.ToString());
            }

            try { Directory.Delete(dest, true); } catch (Exception) {; }
            jsonOutput.Add("status", "sucess");
            WriteJSON();
            Environment.Exit(0);
        }

        private static void Terminate(int value, Action callback)
        {
            callback.Invoke();
            Environment.Exit(value);
        }

        public static void DeleteDirectory(string target_dir)
        {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, true);
        }

        private static void CreateDFPCreative(String name, String[] size, long lineitem, long advertiser, String snippet, String destination)
        {
            var s = new Size();
            s.width = Int32.Parse(size[0].Replace('\\', ' ').Replace('"', ' '));
            s.height = Int32.Parse(size[1].Replace('\\', ' ').Replace('"', ' '));

            var c = new CustomCreative[1];
            c[0] = new CustomCreative();
            c[0].name = name;
            c[0].advertiserId = advertiser;
            c[0].size = s;
            c[0].htmlSnippet = snippet;
            c[0].destinationUrl = destination;

            var rc = new Creative[1];

            rc = creativeService.createCreatives(c);
            jsonOutput["id"] = rc[0].id.ToString();
            jsonOutput["dfp"] = "created";

            var lica = new LineItemCreativeAssociation[1];
            lica[0] = new LineItemCreativeAssociation();
            lica[0].creativeId = rc[0].id;
            lica[0].lineItemId = lineitem;
            licaService.createLineItemCreativeAssociations(lica);
        }

        private static string ProcessFolder(string zip, string dest)
        {
            string snippet = "";
            //CheckForMacros(dest);
            var type = DetermineType(dest);
            CleanUpDirectory(dest);
            try
            {
                if (type == TYPE.FLEX)
                    snippet = Flexitive.DealWithFlex(zip, dest);
                else if (type == TYPE.SWIFFY)
                    snippet = Swiffy.DealWithSwiffy(zip, dest);
                else if (type == TYPE.EDGE)
                    snippet = Edge.DealWithEdge(zip, dest);
                else if (type == TYPE.GWD)
                    snippet = Google.DealWithGWD(zip, dest);
                else if (type == TYPE.WHOKNOWS)
                {
                    errstr += "The file isn't in a format that is supported here. Please refer to the Studio team.\n";
                    DeleteWriteExit(dest);
                }
            }
            catch (Exception)
            {
                errstr += "The file isn't in a format that is supported here. Please refer to the Studio team.\n";
                DeleteWriteExit(dest);
            }
            return snippet;
        }

        public static string GetDest(string zip)
        {
            var dest = zip.Remove(zip.Length - 4, 4);
            DirectoryInfo d = new DirectoryInfo(zip);
            dest = d.Parent.FullName + "\\" + System.DateTime.Now.GetTimestamp();
            return dest;
        }

        private static void CheckForMacros(string dest)
        {
            var files = GetFiles(dest);
            if (!IsInFiles(files, new Regex(@"%%DEST_URL%%")) && !IsInFiles(files, new Regex(@"(%%CLICK_URL_UNESC%%|%%CLICK_URL_ESC%%)")))
            {
                errstr += "No DFP macros detected in the creative. Please refer to the client.";
                DeleteWriteExit(dest);
            }
        }

        static bool IsFlex(string path)
        {
            return IsInFiles(GetFiles(path), new Regex(@"<title>Flexitive</title>"));
        }

        static bool IsSwiffy(string path)
        {
            return IsInFiles(GetFiles(path), new Regex(@"<title>Swiffy Output</title>"));
        }

        static bool IsEdge(string path)
        {
            return IsInFiles(GetFiles(path), new Regex(@"EDGE"));
        }

        static bool IsGWD(string path)
        {
            return IsInFiles(GetFiles(path), new Regex(@"Google Web Designer"));
        }

        public static bool IsInFiles(string[] files, Regex pattern)
        {
            var res = false;
            MapOverFiles(files, (f) => { if (pattern.IsMatch(File.ReadAllText(f))) res = true; });
            return res;
        }

        public static TYPE DetermineType(string path)
        {
            if (IsFlex(path))
                return TYPE.FLEX;
            else if (IsSwiffy(path))
                return TYPE.SWIFFY;
            else if (IsEdge(path))
                return TYPE.EDGE;
            else if (IsGWD(path))
                return TYPE.GWD;
            else
                return TYPE.WHOKNOWS;
        }

        static public void DeleteMac(string path)
        {
            try
            {
                Directory.Delete(path + @"/__MACOSX", true);
            }
            catch (Exception)
            {
                ;
            }
        }

        static public void DeleteDSStore(string path)
        {
            var files = Directory.GetFiles(path, ".DS_Store", SearchOption.AllDirectories);
            MapOverFiles(files, (f) => File.Delete(f));
        }

        static public void CleanUpDirectory(string dir)
        {
            DeleteMac(dir);
            DeleteDSStore(dir);
        }

        static public string[] GetFiles(string path, string toFind = "*.html", SearchOption option = SearchOption.AllDirectories)
        {
            var all = Directory.GetFiles(path, toFind, option);
            var res = new List<string>();

            foreach (var i in all)
            {
                if (!i.Contains("__MACOSX"))
                    res.Add(i);
            }
            return res.ToArray();
        }

        static void ExtractZip(string zip, string dest)
        {
            try
            {
                ZipFile.ExtractToDirectory(zip, dest);
            }
            catch (Exception ex)
            {
                errstr += ex.Message + "\n";
                DeleteWriteExit(dest);
            }
        }

        public static void WriteJSON()
        {
            if (!jsonOutput.ContainsKey("multiple"))
                jsonOutput.Add("multiple", "false");
            var j = JsonConvert.SerializeObject(jsonOutput);
            /*fileout.WriteLine(j);*/
            Console.WriteLine(j);
        }

        public static void WriteErr()
        {
            if (String.Equals(errstr, ""))
                return;
            if (fileout == null)
                fileout = Console.Out;
            Dictionary<String, String> a = new Dictionary<String, String>();
            foreach (KeyValuePair<string, string> entry in jsonOutput)
            {
                a.Add(entry.Key, entry.Value);
            }
            a.Add("status", "error");
            a.Add("errstr", errstr);
            var j = JsonConvert.SerializeObject(a);

            /*fileout.WriteLine(j);*/
            Console.WriteLine(j);
        }

        public static string GetGetScript(string text, string from = "</title>")
        {
            return text.Replace(from, from + @"
<script>function getGet (name) {
                var oResult = {};
                if(location.search.length > 0) {
                               var aQueryString = (location.search.substr(1)).split("" & "");
                               for (var i = 0; i < aQueryString.length; i++)
            {
                var aTemp = aQueryString[i].split(""="");
                if (aTemp.length > 1)
                {
                    oResult[aTemp[0]] = unescape(aTemp[1]);
                }
            }
        }
                return oResult[name];
}</script>");
        }

        public static Dictionary<String, String> GetInfo()
        {
            Assembly infoAss = Assembly.LoadFrom(infoDll);
            Type info = infoAss.GetTypes()[0];
            MethodInfo getInfo = info.GetMethod("getInfo");
            object oinfo = Activator.CreateInstance(info);
            return (Dictionary<String, String>)getInfo.Invoke(oinfo, null);
        }

        public static object GetUpload()
        {
            Assembly upAss = Assembly.LoadFrom(upDll);
            Type tup = upAss.GetTypes()[0];
            upload = tup.GetMethod("Upload");
            return Activator.CreateInstance(tup);
        }

        public static void MapOverFiles(string[] files, Action<string> func)
        {
            foreach (var f in files)
                func(f);
        }

        public static void MapOverFileHref(string[] files, Action<string, string> func)
        {
            foreach (var f in files)
            {
                DirectoryInfo d = new DirectoryInfo(f);
                var hrefPath = d.Parent.Parent.Name + @"/" + d.Parent.Name;
                func(f, hrefPath);
            }
        }

        public static string GetIframeScript(string title, string folder = "", string file = "index.html", string clickUrl = "%%CLICK_URL_UNESC%%", Boolean responsive = false, string clickTracker = "clickTracker")
        {
            var width = "\"%%WIDTH%%px\"";
            var height = "\"%%HEIGHT%%px\"";

            if (responsive)
            {
                width = height = "\"100%\"";
            }
            return @"<script>(function(){
	    var a = document.createElement(""iframe"");
	    a.src = """ + DFPUploader.DFPInfo["baseHref"].Replace(@"%%FOLDER%%", title) + folder + file + @"?" + clickTracker + @"="" + encodeURIComponent('" + clickUrl + @"');
            a.width = " + width + @";
            a.height = " + height + @";
            a.frameBorder = ""0"";
            a.scrolling = ""no"";
            document.writeln(a.outerHTML);
        }());</script>".Replace('"', '\'');
        }
    }
}
