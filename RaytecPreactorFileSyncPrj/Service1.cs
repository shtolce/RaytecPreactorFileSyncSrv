using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace RaytecPreactorFileSyncPrj
{
    public partial class Service1 : ServiceBase
    {
        //[Conditional("DEBUG_SERVICE")]
        private static void DebugMode()
        {
            Debugger.Launch();
        }
        PreactorFileObserver observer;
        public Service1()
        {
            InitializeComponent();
            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            DebugMode();
            observer = new PreactorFileObserver();
            Thread observerThread = new Thread(new ThreadStart(observer.Start));
            observerThread.Start();

        }

        protected override void OnStop()
        {
            observer.Stop();
            Thread.Sleep(1000);
        }

    }//Service1


    /// <summary>
    /// Класс обслуживает жизненый цикл службы. Мониторит изменения файла, как триггер для автодоставки на клиентские машины
    /// повторяет автодоставку, если после копирования файлов md5 источника и  клиента не совпадает
    /// </summary>
    internal class PreactorFileObserver
    {
        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern bool WTSSendMessage(
                        IntPtr hServer,
                        [MarshalAs(UnmanagedType.I4)] int SessionId,
                        String pTitle,
                        [MarshalAs(UnmanagedType.U4)] int TitleLength,
                        String pMessage,
                        [MarshalAs(UnmanagedType.U4)] int MessageLength,
                        [MarshalAs(UnmanagedType.U4)] int Style,
                        [MarshalAs(UnmanagedType.U4)] int Timeout,
                        [MarshalAs(UnmanagedType.U4)] out int pResponse,
                        bool bWait);
        public static IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;


        FileSystemWatcher watcher;
        FileSystemWatcher watcherDll;
        bool enabled = true;
        int tryCount = 0;
        bool notCopied = false;
        bool notCopiedDll = false;
        string sourcePath;
        string filter;
        string sourcePathDll;
        string filterDll;
        string destinationPath;
        string destinationPathDll;
        int timeInterval_ms;

        string sourceFullPathChanged;
        string sourceFullPathDllChanged;
        string sourceFileNameChanged;
        string sourceFileNameDllChanged;
        object lockFileRes = new object();

        public PreactorFileObserver()
        {
            sourcePath = ConfigurationManager.AppSettings.Get("SourcePath");
            filter = ConfigurationManager.AppSettings.Get("Filter"); 
            sourcePathDll = ConfigurationManager.AppSettings.Get("SourcePathDll");
            filterDll = ConfigurationManager.AppSettings.Get("FilterDll");
            destinationPath= ConfigurationManager.AppSettings.Get("DestinationPath"); 
            destinationPathDll = ConfigurationManager.AppSettings.Get("DestinationPathDll");

            bool isParseSuccess = int.TryParse(ConfigurationManager.AppSettings.Get("TimeInterval_ms"), out timeInterval_ms);
            if (!isParseSuccess) timeInterval_ms = 1000;

            //string path = "c:\\1.txt";
            watcher = new FileSystemWatcher();
            //string dirName = Path.GetDirectoryName(path);
            //string fileName = Path.GetFileName(path);
            watcher.Path = sourcePath;
            watcher.Filter = filter;
            watcher.Changed += FileChanged;


            //string pathDll = "d:\\2.txt";
            watcherDll = new FileSystemWatcher();
            //string dirNameDll = Path.GetDirectoryName(pathDll);
            //string fileNameDll = Path.GetFileName(pathDll);
            watcherDll.Path = sourcePathDll;
            watcherDll.Filter = filterDll;
            watcherDll.Changed += FileChangedDll;
        }//PreactorFileObserver()
        private string GetMD5HAsh(string path)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(path))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }//GetMD5HAsh

        private void CopyFiles(string fullPath,string fileName)
        {
            FileInfo fInfo = new FileInfo(fullPath);

            try
            {
                fInfo.CopyTo(destinationPath + fileName, true);
                notCopied = false;
            }
            catch (Exception ex)
            {
                notCopied = true;

            }
        }//CopyFiles()

        private void CopyFilesDll(string fullPath, string fileName)
        {
            FileInfo fInfo = new FileInfo(fullPath);

            try
            {
                fInfo.CopyTo(destinationPath + fileName, true);
                notCopiedDll = false;
            }
            catch (Exception ex)
            {
                notCopiedDll = true;

            }
        }//CopyFiles()

        private bool CompareFile(string fullPath, string fileName)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(destinationPath);
            FileInfo fInfo = new FileInfo(fullPath);
            if (!dirInfo.Exists)
                return true;
            if (!fInfo.Exists)
                return true;

            return GetMD5HAsh(fullPath) == GetMD5HAsh(destinationPath + fileName);
        }//CompareFile()

        private bool CompareFileDll(string fullPath, string fileName)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(destinationPath);
            FileInfo fInfo = new FileInfo(fullPath);
            if (!dirInfo.Exists)
                return true;
            if (!fInfo.Exists)
                return true;

            return GetMD5HAsh(fullPath) == GetMD5HAsh(destinationPath + fileName);
        }//CompareFileDll()


        private void FileChanged(object sender, FileSystemEventArgs e)
        {
            #region testMsg
            //string msg = "test";
            //int mlen = msg.Length;
            //int resp = 7;
            //WTSSendMessage(WTS_CURRENT_SERVER_HANDLE, 1, "1234", 4, msg, mlen, 4,0, out resp, false);
            #endregion
            string fullPath = e.FullPath;
            string fileName = e.Name;
            CopyFiles(fullPath, fileName);
            lock (lockFileRes)
            {
                sourceFullPathChanged = fullPath;
                sourceFileNameChanged = fileName;
            }

        }//FileChanged()

        private void FileChangedDll(object sender, FileSystemEventArgs e)
        {
            #region testMsg
            //string msg = "test";
            //int mlen = msg.Length;
            //int resp = 7;
            //WTSSendMessage(WTS_CURRENT_SERVER_HANDLE, 1, "1234", 4, msg, mlen, 4,0, out resp, false);
            #endregion
            string fullPath = e.FullPath;
            string fileName = e.Name;
            CopyFilesDll(fullPath, fileName);
            lock (lockFileRes)
            {
                sourceFullPathDllChanged = fullPath;
                sourceFileNameDllChanged = fileName;
            }

        }//FileChangedDll()

        public void Start()
        {
            string fullPath;
            string fileName;
            string fullPathDll;
            string fileNameDll;

            watcher.EnableRaisingEvents = true;
            watcherDll.EnableRaisingEvents = true;
            while (enabled)
            {
                lock (lockFileRes)
                {
                    fullPath = sourceFullPathChanged;
                    fileName = sourceFileNameChanged;
                    fullPathDll = sourceFullPathDllChanged;
                    fileNameDll = sourceFileNameDllChanged;
                }
                bool retry = notCopied == true ? !CompareFile(fullPath, fileName): notCopied;

                if (retry == true)
                {
                    CopyFiles(fullPath, fileName);
                }//if
                retry = notCopiedDll == true ? !CompareFileDll(fullPathDll, fileNameDll): notCopiedDll;
                if (retry == true)
                {
                    CopyFilesDll(fullPathDll, fileNameDll);
                }//if
                Thread.Sleep(timeInterval_ms);

            }//while

        }//Start()

        public void Stop()
        {
            watcher.EnableRaisingEvents = false;
            enabled = false;
        }

    }//class PreactorFileObserver











}//ns
