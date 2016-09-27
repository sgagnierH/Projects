using System;
using System.Threading;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using WinSCP;
using System.Management;
using System.Diagnostics;

namespace DFPUploader
{
    public class Uploader
    {
        string _lastFileName;

        public void Upload(string root, string path, Dictionary<String, String> DFPInfo)
        {
            try
            {
                var opt = new SessionOptions
                {
                    Protocol = Protocol.Sftp,
                    HostName = DFPInfo["url"],
                    UserName = DFPInfo["user"],
                    SshHostKeyFingerprint = "ssh-dss 1024 ee:33:bd:ac:7b:6e:bd:0b:60:6e:49:20:56:cb:00:d3",
                    SshPrivateKeyPath = DFPInfo["ppkPath"]
                };

                using (var session = new Session())
                {
                    //session.FileTransferProgress += SessionFileTransferProgress;
                    session.Open(opt);
                    var transferOptions = new TransferOptions();
                    transferOptions.FileMask = "*";
                    transferOptions.TransferMode = TransferMode.Binary;
                    transferOptions.ResumeSupport.State = TransferResumeSupportState.Off;

                    var result = session.PutFiles(path + @"\", DFPInfo["baseDir"] + root, false, transferOptions);

                    result.Check();
                }
                //Console.WriteLine("Done");
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return;
        }

        void SessionFileTransferProgress(object sender, FileTransferProgressEventArgs e)
        {
            if ((_lastFileName != null) && (_lastFileName != e.FileName))
            {
                Console.WriteLine();
            }

            Console.Write("\r{0} ({1:P0})", e.FileName, e.FileProgress);
            _lastFileName = e.FileName;
        }
    }
}
