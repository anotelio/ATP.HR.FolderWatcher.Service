using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Dapper;

namespace ATP.HR.FolderWatcher.Service
{
    public enum StatusTypes : int
    {
        Bootstrap = 1,
        Running = 2,
        Failed = 3,
        Successfull = 4,
        ExecutionNotPermitted = 5,
        FailedDueToClusterFailover = 6
    };

    public class DatabaseManager
    {
        private string processStatusProcedureName = "[Process].[etl_hr_files_import_reporting_process_step_check]";
        private string processStatusProcedureParamName01 = "process_name";
        private string processStatusProcedureParamName02 = "run_date";

        public string processStep1Name = "HR_User_Department_Map_Import";
        public string processStep2Name = "HR_Zeus_Files_Import";
        public string processStep3Name = "HR_Data_Calc_Rep";

        private string jobRunProcedureName = "[run].[etl_hr_files_import_reporting_job_exec]";
        private string jobRunProcedureParamName01 = "job_step_name";

        public string jobStep1Name = "RunMCP_User_Department_Map";
        public string jobStep2Name = "RunMCP_Import_Zeus_Files";
        public string jobStep3Name = "RunMCP_Calc_Report";

        public string jobName = "ETL - HR - FilesImport - Reporting";
        private string jobStatusProcedureName = "[run].[etl_hr_files_import_reporting_job_status_get]";
        private string jobStatusProcedureParamName01 = "job_step_name";
        private string jobStatusProcedureParamOutputName01 = "isJobRunning";

        public List<CoreProcessStatusDto> GetProcessStatusIds(string ProcessName, DateTime dateTime)
        {
            var parameters = new DynamicParameters();
            parameters.Add(processStatusProcedureParamName01, ProcessName);
            parameters.Add(processStatusProcedureParamName02, dateTime);

            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(AppConfig.CoreDbProcessesConnection))
            {
                var output = connection. Query<CoreProcessStatusDto>(processStatusProcedureName, parameters, commandType: CommandType.StoredProcedure).ToList();

                return output;
            }
        }

        public void RunHrJob(string jobStepName)
        {
            var parameters = new DynamicParameters();
            parameters.Add(jobRunProcedureParamName01, jobStepName);

            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(AppConfig.HrReportDbConnection))
            {
                connection.Execute(jobRunProcedureName, parameters, commandType: CommandType.StoredProcedure);
            }
        }

        public bool GetJobStatus(string jobName)
        {
            var parameters = new DynamicParameters();
            parameters.Add(jobStatusProcedureParamName01, jobName);
            parameters.Add(jobStatusProcedureParamOutputName01, dbType: DbType.Boolean, direction: ParameterDirection.Output);

            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(AppConfig.HrReportDbConnection))
            {
                connection.Execute(jobStatusProcedureName, parameters, commandType: CommandType.StoredProcedure);
                var output = parameters.Get<bool>(jobStatusProcedureParamOutputName01);
                return output;
            }
        }

        public bool IsFailureProcessStatus(List<CoreProcessStatusDto> statusTypesList, string processName, DateTime dateTime, int secondsElapsed, int packageCount)
        {
            bool isStepFailure = true;

            Stopwatch doStepUntil = new Stopwatch();
            doStepUntil.Start();

            while (doStepUntil.Elapsed < TimeSpan.FromSeconds(secondsElapsed))
            {

                statusTypesList = this.GetProcessStatusIds(processName, dateTime);
                var statusTypesStepSelection = statusTypesList.Select(st => st.ProcessStatusTypeId).ToList();

                if (new [] { StatusTypes.Failed, StatusTypes.ExecutionNotPermitted, StatusTypes.FailedDueToClusterFailover }
                    .Any(s => statusTypesStepSelection.Contains((int)s)))
                {
                    isStepFailure = true;
                    return isStepFailure;
                }
                else
                if (statusTypesStepSelection.Contains((int)StatusTypes.Running) ||
                    statusTypesStepSelection.Contains((int)StatusTypes.Bootstrap) ||
                    statusTypesStepSelection.Contains(null))
                {
                    continue;
                }
                else
                if (statusTypesStepSelection.Count() == packageCount)
                {
                    isStepFailure = false;
                    return isStepFailure;
                }
                else
                {
                    isStepFailure = true;
                    return isStepFailure;
                }

            }

            doStepUntil.Stop();

            return isStepFailure;
        }

        public bool IsJobRunning(string jobName, int secondsElapsed)
        {
            bool isJobRunning = true;

            Stopwatch doUntil = new Stopwatch();
            doUntil.Start();

            while (doUntil.Elapsed < TimeSpan.FromSeconds(secondsElapsed))
            {
                var e = GetJobStatus(jobName);
                isJobRunning = GetJobStatus(jobName);

                if (isJobRunning)
                {
                    continue;
                }
                else
                {
                    break;
                }
            }

            doUntil.Stop();

            return isJobRunning;
        }
    }
}
