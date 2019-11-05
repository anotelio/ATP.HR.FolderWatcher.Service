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
        private FileSystemWatcher watcher;

        private List<string> filesToProcessList;

        private readonly string receivedDataPath;

        private readonly string processedDataPath;

        private List<string> filesToMoveNamesList;

        private List<string> filesToNotDeleteNamesList;

        private DirectoryInfo watchDirectoryInfo;

        private DirectoryInfo processDirectoryInfo;

        private string fileToProcessedName01;

        public FolderWatcher(string receivedDataPath, string processedDataPath)
        {
            this.receivedDataPath = receivedDataPath;
            this.processedDataPath = processedDataPath;
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
                bool isDone = false;
                string dateTimeNow;

                dateTimeNow = DateTime.Now.ToString("MM.dd.yyyy_HH-mm-ss.fff");

                bool spinUntil = SpinWait.SpinUntil(() =>
                {
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
                                        dateTimeNow,
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
                }, TimeSpan.FromSeconds(5));

                Console.WriteLine((spinUntil == true && isMoved == true) ? "File was Moved!" : "Failed and canceled.");

                bool isDone2 = false;

                bool spinUntil2 = SpinWait.SpinUntil(() =>
                {
                    try
                    {
                        if (isMoved)
                        {
                            FileSystemManager.ClearFolderFromUselessFiles(processDirectoryInfo, filesToProcessList.Concat(filesToNotDeleteNamesList).ToArray());

                            FileInfo[] processCurrentFilesInfo = processDirectoryInfo.GetFiles();

                            foreach (FileInfo fileInfo in processCurrentFilesInfo)
                            {
                                if (fileInfo.Extension == ".zip" && filesToProcessList.ToArray().Contains(fileInfo.Name))
                                {
                                    if (fileInfo.Name == string.Concat(fileToProcessedName01.Left(fileToProcessedName01.Length - 4), "_", dateTimeNow, ".zip"))
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
                                            Console.WriteLine("Missing files:");
                                            foreach (string value in missingFilesList)
                                            {
                                                Console.WriteLine(value);
                                            }
                                        }
                                    }
                                }
                            }
                            FileSystemManager.ClearFolderFromUselessFiles(watchDirectoryInfo, filesToMoveNamesList.ToArray());
                        }

                        isDone2 = true;
                    }
                    catch
                    {
                    }

                    return isDone2;
                }, TimeSpan.FromSeconds(5));

                Console.WriteLine((spinUntil2 == true && isExtracted == true) ? "File was Extracted!" : "Failed and canceled.2");
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
            this.Watch(this.receivedDataPath, this.processedDataPath);
            this.watcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            watcher.EnableRaisingEvents = false;
            this.Dispose();
        }
    }
}