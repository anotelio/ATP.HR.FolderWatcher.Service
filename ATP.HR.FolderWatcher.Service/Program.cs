using System;
using Topshelf;
using Topshelf.FileSystemWatcher;

namespace ATP.HR.FolderWatcher.Service
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var exitCode = HostFactory.Run(r =>
                {
                    r.Service<FolderWatcher>(s =>
                    {
                        s.ConstructUsing(f => new FolderWatcher("D:\\Temp\\hr_data", "hr_data"));
                        s.WhenStarted(f => f.Start());
                        s.WhenStopped(f => f.Stop());
                    });

                    r.RunAsLocalSystem();
                    r.StartAutomatically();

                    r.SetServiceName("ATP.HR.FolderWatcher.Service");
                    r.SetDisplayName("ATP.HR.FolderWatcher.Service");
                    r.SetDescription("This is the service for folder watching zeus files in order to start SQL agent job with SSIS solutions which load these files into hr_reporting database.");
                });

                int exitCodeValue = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
                Environment.ExitCode = exitCodeValue;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.ReadKey();
            }
        }
    }
}
