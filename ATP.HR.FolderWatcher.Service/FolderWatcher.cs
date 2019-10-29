using System;
using System.IO;
using System.Threading;

namespace ATP.HR.FolderWatcher.Service
{
    public class FolderWatcher
    {
        private FileSystemWatcher watcher;
        private readonly string folderPath;

        public FolderWatcher(string folderPath)
        {
            this.folderPath = folderPath;
        }

        private void Watch(string folderPath)
        {
            watcher = new FileSystemWatcher();
            this.watcher.Path = folderPath;
            this.watcher.NotifyFilter = NotifyFilters.LastWrite;
            this.watcher.Filter = "*.csv";
            this.watcher.Changed += OnChanged;
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            try
            {
                this.watcher.EnableRaisingEvents = false;

                FileInfo file = new FileInfo("D:\\Temp\\hr_data\\url_nichtstempler.csv");

                bool spinUntil = SpinWait.SpinUntil(() => {
                    bool isMoved = false;

                    try
                    {
                        file.Delete();
                        isMoved = true;
                    }
                    catch
                    {
                    }

                    return isMoved;
                }, TimeSpan.FromSeconds(5));

                

                Console.WriteLine(spinUntil ? "File Moved!" : "Failed and canceled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("File Not Moved! Error:" + ex.Message);
            }
            finally
            {
                this.watcher.EnableRaisingEvents = true;
            }
        }

        private void Dispose()
        {
            // avoiding resource leak
            this.watcher.Changed -= OnChanged;
            this.watcher.Dispose();
        }

        public void Start()
        {
            this.Watch(this.folderPath);
            this.watcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            watcher.EnableRaisingEvents = false;
            this.Dispose();
        }
    }
}
