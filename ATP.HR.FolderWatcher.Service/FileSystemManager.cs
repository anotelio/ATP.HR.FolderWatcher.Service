using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATP.HR.FolderWatcher.Service
{
    public static class FileSystemManager
    {
        public static string receivedFileIgnoredName01 = "hr_data_files.zip";
        public static string folderIgnoredName01 = "loaded_files";

        //public static Dictionary<string, FileInfo> FilesToProcessed;

        public static List<string> filesToProcessedList = new List<string>()
        {
            "user_department_maps.xlsx",
            "Gesamtsoll.xls",
            "Gesamtüstd..xls",
            "inaktiv.xls",
            "krank h Nichtstempler KML.xls",
            "krank h Nichtstempler KOL.xls",
            "Krank inh h - KML.xls",
            "Krank inh h - KOL.xls",
            "Netto-Arbeitszeit-Abteilung.xls",
            "schwerbehindert.xls",
            "Sonstige Fehlzeiten.xls",
            "Sonstige Krank in h.xls",
            "Übersicht Nichtstempler.xls",
            "URL Nichtstempler.xls",
            "Urlaubsstunden.xls",
            "Lohnliste.xlsx"
        };

        public static void ClearFolderFromUselessFiles(DirectoryInfo currentDirectoryInfo, params string[] ignoredNames)
        {
            FileInfo[] fileInfos;
            DirectoryInfo[] directoryInfos;

            fileInfos = currentDirectoryInfo.GetFiles().ToArray();

            directoryInfos = currentDirectoryInfo.GetDirectories().ToArray();

            var fileItems = fileInfos
                .Where(item => !ignoredNames?.Contains(item.Name) ?? true)
                .ToArray();

            var dirItems = directoryInfos
                .Where(item => !ignoredNames?.Contains(item.Name) ?? true)
                .ToArray();

            foreach (var item in fileItems)
            {
                item.Delete();
            }

            foreach (var item in dirItems)
            {
                Directory.Delete(item.FullName, true);
            }
        }
    }
}