using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATP.HR.FolderWatcher.Service
{
    public static class AppConfig
    {
        public static string HrReportDbConnection => ConfigurationManager.ConnectionStrings["hrReportDb"].ConnectionString;

        public static string CoreDbProcessesConnection => ConfigurationManager.ConnectionStrings["coreDbProcesses"].ConnectionString;
    }
}
