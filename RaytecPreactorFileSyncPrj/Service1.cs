using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        public PreactorFileObserver()
        {
            string path = "c:\\1.txt";
            watcher = new FileSystemWatcher();
            string dirName = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);
            watcher.Path = dirName;
            watcher.Filter = fileName;
            watcher.Changed += FileChanged;


            string pathDll = "d:\\2.txt";
            watcherDll = new FileSystemWatcher();
            string dirNameDll = Path.GetDirectoryName(pathDll);
            string fileNameDll = Path.GetFileName(pathDll);
            watcherDll.Path = dirNameDll;
            watcherDll.Filter = fileNameDll;
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
                fInfo.CopyTo("c:\\zzz\\" + fileName, true);
                notCopied = false;
            }
            catch (Exception ex)
            {
                notCopied = true;

            }
        }//CopyFiles()

        private bool CompareFile(string fullPath, string fileName)
        {
            return GetMD5HAsh(fullPath) == GetMD5HAsh("c:\\zzz\\" + fileName);
        }//CompareFile()

        private bool CompareFileDll(string fullPath, string fileName)
        {
            return GetMD5HAsh(fullPath) == GetMD5HAsh("c:\\zzz\\" + fileName);
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
            CopyFiles(fullPath, fileName);

        }//FileChangedDll()

        public void Start()
        {
            watcher.EnableRaisingEvents = true;
            watcherDll.EnableRaisingEvents = true;
            while (enabled)
            {
                Thread.Sleep(1000);
                string fullPath = "c:\\1.txt";
                string fileName = "1.txt";
                string fullPathDll = "d:\\2.txt";
                string fileNameDll = "2.txt";
                notCopied = !CompareFile(fullPath, fileName) || !CompareFileDll(fullPathDll, fileNameDll);
                if (notCopied==true)
                {
                    CopyFiles(fullPath, fileName);
                }//if

            }//while

        }//Start()

        public void Stop()
        {
            watcher.EnableRaisingEvents = false;
            enabled = false;
        }

    }//class PreactorFileObserver











}//ns
