using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tc.TcMedia.Scheduler;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

namespace CSVDump
{
    public class CSVDump : iScheduler
    {
        public void Run(Process process)
        {
            Command cmd = JsonConvert.DeserializeObject<Command>(process.schedule.Config);

            StringBuilder sb = new StringBuilder();
            long nbRows = 0;

            process.log("Writing CSV File");
            process.log(process.schedule.Config);
            StringBuilder line = new StringBuilder();

            MySqlDataReader dataReader = Db.getMySqlReader(process, cmd.query);
            while (dataReader.Read())
            {                
                if (sb.ToString() == "") // Header
                {
                    for (int i = cmd.startColumn(); i < dataReader.FieldCount; i++)
                        line.Append(((line.Length == 0) ? "" : ",") + cmd.stringIndicator + dataReader.GetName(i) + cmd.stringIndicator);

                    sb.AppendLine(line.ToString());
                }
                nbRows++;

                line.Clear();
                for (int i = cmd.startColumn(); i < dataReader.FieldCount; i++)
                {
                    string type = dataReader[i].GetType().Name;
                    line.Append((line.Length == 0) ? "" : ",");
                    line.Append((type == "String" || type == "DateTime" || cmd.quoteAll()) ? cmd.stringIndicator : "");
                    line.Append(dataReader.GetValue(i).ToString().Replace("'", "''"));
                    line.Append((type == "String" || type == "DateTime" | cmd.quoteAll()) ? cmd.stringIndicator : "");
                }
                sb.AppendLine(line.ToString());
            }

            sb.Append(cmd.footer);

            dataReader.Close();

            if (nbRows > 0)
            {
                string filename = fixFilename(cmd.filename);
                try
                {
                    string zipFilename = filename.Replace("csv", "zip");
                    if (File.Exists(filename)) File.Delete(filename);

                    File.WriteAllText(filename, sb.ToString());

                    if(cmd.compressIt())
                    {
                        if (File.Exists(zipFilename)) File.Delete(zipFilename);

                        using( var memoryStream = new MemoryStream())
                        {
                            using ( var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                            {
                                var file = archive.CreateEntry(filename.Substring(filename.LastIndexOf("\\") + 1));

                                using (var entryStream = file.Open())
                                using (var streamWriter = new StreamWriter(entryStream))
                                {
                                    streamWriter.Write(sb.ToString());
                                }
                            }
                            using (var fileStream = new FileStream(zipFilename, FileMode.Create))
                            {
                                memoryStream.Seek(0,SeekOrigin.Begin);
                                memoryStream.CopyTo(fileStream);
                            }
                        }

                        if (File.Exists(filename)) File.Delete(filename);
                        filename = zipFilename;
                    }
                    Db.sendMail(process, cmd.toEmail, cmd.title, cmd.body, filename);

                    if (File.Exists(filename)) File.Delete(filename);
                }
                catch(Exception ex)
                {
                    process.log("Unable to process file " + filename);
                    throw new Exception("Unable to process file " + filename, ex);
                }
            }
            else
                Db.sendMail(process, cmd.toEmail, cmd.title, cmd.nothingToDo);
        }
        public static string fixFilename(string filename)
        {
            string ret = "";

            if (filename.IndexOf("yyyyMMdd") > 0)
            {
                ret = filename.Replace("yyyyMMdd", System.DateTime.Now.ToString("yyyyMMdd"));
                filename = ret;
            }

            if (filename.IndexOf("%rnd%") > 0)
            {
                ret = filename.Replace("%rnd%", (System.DateTime.Now.Ticks % 1000000).ToString());
                filename = ret;
            }
            if (filename.IndexOf("date[") > 0)
            {
                string pattern = "%date\\[(.*)\\]%";
                Match match = Regex.Match(filename, pattern);

                Regex reg = new Regex(pattern);
                ret = reg.Replace(filename, System.DateTime.Now.ToString(match.Groups[1].Value));

                filename = ret;
            }
            return ret;
        }
    }
    public class Command
    {
        public string query { get; set;}
        public string filename { get; set; }
        public string title { get; set; }
        public string body { get; set; }
        public string toEmail { get; set; }
        public string nothingToDo { get; set; }
        public string compress { get; set; }
        public string stringIndicator { get; set; }
        public string startingAt { get; set; }
        public string allString { get; set; }
        public string footer { get; set; }

        public Command()
        {
            nothingToDo = "No data to process";
            stringIndicator = "'";
            startingAt = "1";
        }
        public bool compressIt()
        {
            bool ret = false;
            if (compress == "1") ret = true;
            return ret;
        }
        public int startColumn()
        {
            return Convert.ToInt16(startingAt);
        }
        public bool quoteAll()
        {
            return allString == "1";
        }
    }
}
