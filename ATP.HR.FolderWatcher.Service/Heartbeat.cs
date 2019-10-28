using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace ATP.HR.FolderWatcher.Service
{
    public class Heartbeat
    {
        private readonly Timer timer;

        public Heartbeat()
        {
            timer = new Timer(1000) { AutoReset = true };
            timer.Elapsed += timerElapsed;
        }

        private void timerElapsed(object sender, ElapsedEventArgs e)
        {
            string[] lines = new string[] { DateTime.Now.ToString() };
            File.AppendAllLines(@"C:\Temp\Demos\Heartbeat.txt", lines);
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
