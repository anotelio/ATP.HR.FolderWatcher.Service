using System;
using System.Collections;
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

        //public static Dictionary<string, string> receivedFilesAll;

        private readonly string receivedDataPath;

        private readonly string processedDataPath;

        private List<string> filesToMoveNamesList;

        private List<string> filesToNotDeleteNamesList;

        private DirectoryInfo watchDirectoryInfo;

        private DirectoryInfo processDirectoryInfo;

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

            this.filesToMoveNamesList.Add(FileSystemManager.receivedFileIgnoredName01);
            this.filesToNotDeleteNamesList.Add(FileSystemManager.folderIgnoredName01);

            FileSystemManager.ClearFolderFromUselessFiles(processDirectoryInfo, filesToNotDeleteNamesList.ToArray());
            FileSystemManager.ClearFolderFromUselessFiles(watchDirectoryInfo);

            this.watcher = new FileSystemWatcher();
            this.watcher.Path = receivedDataPath;
            this.watcher.NotifyFilter = NotifyFilters.LastWrite;
            this.watcher.Changed += OnChanged;
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            try
            {
                this.watcher.EnableRaisingEvents = false;

                bool isMoved = false;

                bool spinUntil = SpinWait.SpinUntil(() =>
                {
                    bool isDone = false;
                    
                    try
                    {
                        if (filesToMoveNamesList.Contains(e.Name))
                        {
                            if (File.Exists(e.FullPath))
                            {
                                if (File.Exists(Path.Combine(processedDataPath, e.Name)))
                                {
                                    File.Delete(Path.Combine(processedDataPath, e.Name));
                                }

                                File.Move(e.FullPath, Path.Combine(processedDataPath, e.Name));
                                isMoved = true;
                            }
                        }
                        else
                        {
                            foreach (string item in filesToMoveNamesList)
                            {
                                string receiveFilePath = Path.Combine(receivedDataPath, item);
                                string processFilePath = Path.Combine(processedDataPath, item);

                                if (File.Exists(receiveFilePath))
                                {
                                    if (File.Exists(processFilePath))
                                    {
                                        File.Delete(processFilePath);
                                    }

                                    File.Move(receiveFilePath, processFilePath);
                                    isMoved = true;
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
                }, TimeSpan.FromSeconds(5));

                if (isMoved)
                {
                    FileSystemManager.ClearFolderFromUselessFiles(processDirectoryInfo, filesToMoveNamesList.Concat(filesToNotDeleteNamesList).ToArray());

                    foreach (FileInfo fileInfo in processDirectoryInfo.GetFiles())
                    {
                        if (fileInfo.Extension == ".zip" && filesToMoveNamesList.ToArray().Contains(fileInfo.Name))
                        {
                            if (fileInfo.Name == FileSystemManager.receivedFileIgnoredName01)
                            {
                                List<string> currentZipFilesList = ZipFile.OpenRead(fileInfo.FullName)
                                    .Entries
                                    .Select(cz => cz.FullName)
                                    .ToList();

                                IEnumerable<string> compareList;

                                compareList = FileSystemManager.filesToProcessedList.Except(currentZipFilesList);

                                foreach (string value in compareList)
                                {
                                    Console.WriteLine(value);
                                }

                                ZipFile.ExtractToDirectory(fileInfo.FullName, fileInfo.DirectoryName);
                            }
                        }
                    }
                }

                Console.WriteLine((spinUntil == true && isMoved == true) ? "File Moved!" : "Failed and canceled.");
                
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