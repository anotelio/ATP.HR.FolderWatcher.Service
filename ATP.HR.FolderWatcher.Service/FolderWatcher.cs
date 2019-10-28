using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Topshelf.FileSystemWatcher;

namespace ATP.HR.FolderWatcher.Service
{
    public class FolderWatcher
    {
        private readonly Timer timer;
        
        private readonly FileSystemEventArgs fileSystemFolderChangedArgs;

        public FolderWatcher(string folderPath, string folderName)
        {
            this.fileSystemFolderChangedArgs = new FileSystemEventArgs(WatcherChangeTypes.Changed, folderPath, folderName);
            FileSystemEventFactory.CreateNormalFileSystemEvent(fileSystemFolderChangedArgs);

            timer = new Timer(1000) { AutoReset = true };
            timer.Elapsed += TimerElapsed;
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
        }

        public void Start()
        {
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
        }
    }
}
