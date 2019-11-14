using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.IO.Compression;

namespace ATP.HR.FolderWatcher.Service
{
    public class FolderWatcher
    {
        #region Private_Fields
        private readonly int jobSecondsElapsed = 10;

        private FileSystemWatcher watcher;

        private List<string> filesToProcessList;

        private readonly string receivedDataPath;

        private readonly string processedDataPath;

        private List<string> filesToMoveNamesList;

        private List<string> filesToNotDeleteNamesList;

        private DirectoryInfo watchDirectoryInfo;

        private DirectoryInfo processDirectoryInfo;

        private string fileToProcessedName01;

        private List<CoreProcessStatusDto> statusTypesList;

        private DatabaseManager databaseManager;

        private string fileToProcessedName = string.Empty;
        #endregion

        #region Private_Events
        private event Action<bool> Step1Success;

        private event Action<bool> Step2Success;

        private event Action<bool> Step3Success;
        #endregion

        public FolderWatcher(string receivedDataPath, string processedDataPath)
        {
            this.receivedDataPath = receivedDataPath;
            this.processedDataPath = processedDataPath;

            StepsFlow();
        }

        private void StepsFlow()
        {
            this.Step1Success += (result) =>
            {
                if (result)
                {
                    this.RunSecondStep();
                }
            };

            this.Step2Success += (result) =>
            {
                if (result)
                {
                    this.RunThirdStep();
                }
            };

            this.Step3Success += (result) =>
            {
                bool isSaved = false;

                if (result)
                {
                    Console.WriteLine("Finished successfully. Steps were not failed.");

                    bool spinUntilSave = SpinWait.SpinUntil(() =>
                        SavedProcessedFiles(ref isSaved), TimeSpan.FromSeconds(5));

                    Console.WriteLine((spinUntilSave == true && isSaved == true) ? "File was Moved and Saved to archive folder!" : "Failed and canceled while saving to archive folder");

                    FileSystemManager.ClearFolderFromUselessFiles(processDirectoryInfo, filesToNotDeleteNamesList.ToArray());
                }
                else
                {
                    Console.WriteLine("Steps Failed!");
                }
            };
        }

        private void Watch(string receivedDataPath, string processedDataPath)
        {
            this.watchDirectoryInfo = new DirectoryInfo(receivedDataPath);

            this.processDirectoryInfo = new DirectoryInfo(processedDataPath);

            filesToMoveNamesList = new List<string>();
            filesToNotDeleteNamesList = new List<string>();

            this.fileToProcessedName01 = FileSystemManager.receivedFileIgnoredName01;

            this.filesToMoveNamesList.Add(fileToProcessedName01);
            this.filesToNotDeleteNamesList.Add(FileSystemManager.folderIgnoredName01);

            FileSystemManager.ClearFolderFromUselessFiles(processDirectoryInfo, filesToNotDeleteNamesList.ToArray());
            FileSystemManager.ClearFolderFromUselessFiles(watchDirectoryInfo);

            statusTypesList = new List<CoreProcessStatusDto>();

            this.watcher = new FileSystemWatcher()
            {
                Path = receivedDataPath,
                NotifyFilter = NotifyFilters.LastWrite,
                //Filter = "*.zip"
            };

            this.watcher.Changed += new FileSystemEventHandler(OnChanged);
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            try
            {
                this.watcher.EnableRaisingEvents = false;

                this.filesToProcessList = new List<string>();

                bool isMoved = false;
                bool isExtracted = false;
                string dateTimeNow;

                dateTimeNow = DateTime.Now.ToString("MM.dd.yyyy_HH-mm-ss.fff");

                bool spinUntilMove = SpinWait.SpinUntil(() =>
                    MoveReceivedFiles(dateTimeNow, ref isMoved), TimeSpan.FromSeconds(5));

                this.fileToProcessedName = string.Concat(fileToProcessedName01.Left(fileToProcessedName01.Length - 4), "_", dateTimeNow, ".zip");

                if (spinUntilMove == true && isMoved == true)
                {
                    Console.WriteLine("File was Moved!");

                    bool spinUntilExtract = SpinWait.SpinUntil(() =>
                        ExtractProcessFiles(this.fileToProcessedName, ref isExtracted), TimeSpan.FromSeconds(5));

                    Console.WriteLine((spinUntilExtract == true && isExtracted == true) ? "File was Extracted!" : "Failed and canceled while extracting file.");
                }
                else
                {
                    Console.WriteLine("Failed and canceled while moving some received files.");
                }

                if (isExtracted)
                {
                    databaseManager = new DatabaseManager();

                    this.RunFirstStep();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                this.watcher.EnableRaisingEvents = true;
            }
        }

        public void Start()
        {
            this.Watch(this.receivedDataPath, this.processedDataPath);
            this.watcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            watcher.EnableRaisingEvents = false;
            this.Dispose();
        }

        private void Dispose()
        {
            // avoiding resource leak
            this.watcher.Changed -= OnChanged;
            this.watcher.Dispose();
        }

        private bool MoveReceivedFiles(string strDateTimeNow, ref bool isMoved)
        {
            bool isDone = false;

            try
            {
                FileInfo[] receiveCurrentFilesInfo = watchDirectoryInfo.GetFiles();
                string receiveFilePath;
                string processFilePath;

                Thread.Sleep(5); // 5ms

                foreach (FileInfo fileInfo in receiveCurrentFilesInfo)
                {
                    if (filesToMoveNamesList.ToArray().Contains(fileInfo.Name))
                    {
                        receiveFilePath = fileInfo.FullName;
                        processFilePath = Path.Combine(processDirectoryInfo.FullName,
                            string.Concat(Path.GetFileNameWithoutExtension(receiveFilePath),
                                "_",
                                strDateTimeNow,
                                Path.GetExtension(receiveFilePath)));

                        if (File.Exists(receiveFilePath))
                        {
                            File.Move(receiveFilePath, processFilePath);

                            filesToProcessList.Add(Path.GetFileName(processFilePath));

                            isMoved = true;
                        }
                    }
                }

                isDone = true;
            }
            catch (Exception rp)
            {
                Console.WriteLine(rp);
            }

            return isDone;
        }

        private bool ExtractProcessFiles(string newFileToProcessedName01, ref bool isExtracted)
        {
            bool isDone = false;

            try
            {
                FileSystemManager.ClearFolderFromUselessFiles(processDirectoryInfo, filesToProcessList.Concat(filesToNotDeleteNamesList).ToArray());

                FileInfo[] processCurrentFilesInfo = processDirectoryInfo.GetFiles();

                foreach (FileInfo fileInfo in processCurrentFilesInfo)
                {
                    if (fileInfo.Extension == ".zip" && filesToProcessList.ToArray().Contains(fileInfo.Name))
                    {
                        if (fileInfo.Name == newFileToProcessedName01)
                        {
                            List<string> currentZipFilesList = ZipFile.OpenRead(fileInfo.FullName)
                                .Entries
                                .Select(cz => cz.FullName)
                                .ToList();

                            IEnumerable<string> missingFilesList;

                            missingFilesList = FileSystemManager.filesToProcessedList.Except(currentZipFilesList);

                            if (missingFilesList.Count() == 0 && currentZipFilesList.Count() == FileSystemManager.filesToProcessedList.Count())
                            {
                                ZipFile.ExtractToDirectory(fileInfo.FullName, fileInfo.DirectoryName);

                                isExtracted = true;
                            }
                            else
                            {
                                Console.WriteLine("Missing files in .zip file or there are some extra files:");
                                foreach (string value in missingFilesList)
                                {
                                    Console.WriteLine(value);
                                }
                            }
                        }
                    }
                }

                FileSystemManager.ClearFolderFromUselessFiles(watchDirectoryInfo, filesToMoveNamesList.ToArray());

                isDone = true;
            }
            catch
            {
            }

            return isDone;
        }

        private void RunFirstStep()
        {
            bool result = true;
            bool isFailure = true;
            bool isJobRunning = true;

            isJobRunning = databaseManager.IsJobRunning(databaseManager.jobName, this.jobSecondsElapsed);

            if (isJobRunning)
            {
                Console.WriteLine("Pre-execution job check on Step1 failed. Job is running or stuck.");
                result = false;
            }
            else
            {

                databaseManager.RunHrJob(databaseManager.jobStep1Name);

                int step1SecondsElapsed = 30;
                int step1PackageCount = 2;

                isFailure = databaseManager.IsFailureProcessStatus(statusTypesList, databaseManager.processStep1Name, DateTime.Now, step1SecondsElapsed, step1PackageCount);

                if (isFailure)
                {
                    Console.WriteLine("Step1 failed.");
                    result = false;
                }
                else
                {
                    Thread.Sleep(1000);

                    isJobRunning = databaseManager.IsJobRunning(databaseManager.jobName, this.jobSecondsElapsed);

                    if (isJobRunning)
                    {
                        Console.WriteLine("Job on Step1 is finishing too long.");
                        result = false;
                    }
                    else
                    {
                        Console.WriteLine("Step1 is finished successfully.");
                    }
                }
            }

            this.Step1Success(result);
        }

        private void RunSecondStep()
        {
            bool result = true;
            bool isFailure = true;
            bool isJobRunning = true;

            databaseManager.RunHrJob(databaseManager.jobStep2Name);

            int step2SecondsElapsed = 60 * 2;
            int step2PackageCount = 16;

            isFailure = databaseManager.IsFailureProcessStatus(statusTypesList, databaseManager.processStep2Name, DateTime.Now, step2SecondsElapsed, step2PackageCount);

            if (isFailure)
            {
                Console.WriteLine("Step2 failed.");
                result = false;
            }
            else
            {
                Thread.Sleep(1000);

                isJobRunning = databaseManager.IsJobRunning(databaseManager.jobName, this.jobSecondsElapsed);

                if (isJobRunning)
                {
                    Console.WriteLine("Job on Step2 is finishing too long.");
                    result = false;
                }
                else
                {
                    Console.WriteLine("Step2 is finished successfully.");
                }
            }

            this.Step2Success(result);
        }

        private void RunThirdStep()
        {
            bool result = true;
            bool isFailure = true;
            bool isJobRunning = true;

            databaseManager.RunHrJob(databaseManager.jobStep3Name);

            int step3SecondsElapsed = 60 * 5;
            int step3PackageCount = 2;

            isFailure = databaseManager.IsFailureProcessStatus(statusTypesList, databaseManager.processStep3Name, DateTime.Now, step3SecondsElapsed, step3PackageCount);

            if (isFailure)
            {
                Console.WriteLine("Step3 failed.");
                result = false;
            }
            else
            {
                Console.WriteLine("Step3 is finished successfully.");
            }

            isJobRunning = databaseManager.IsJobRunning(databaseManager.jobName, this.jobSecondsElapsed);

            if (isJobRunning)
            {
                Console.WriteLine("Job on Step3 is finishing too long. Please check.");
            }

            this.Step3Success(result);
        }

        private bool SavedProcessedFiles(ref bool isSaved)
        {
            bool isDone = false;
            string sourceProcessedPathNew = Path.Combine(processDirectoryInfo.FullName, this.fileToProcessedName);

            try
            {

                if (File.Exists(sourceProcessedPathNew))
                {
                    File.Move(sourceProcessedPathNew,
                        Path.Combine(processDirectoryInfo.FullName, FileSystemManager.folderIgnoredName01, this.fileToProcessedName));

                    isSaved = true;
                }

                isDone = true;
            }
            catch (Exception rp)
            {
                Console.WriteLine(rp);
            }

            return isDone;
        }
    }
}