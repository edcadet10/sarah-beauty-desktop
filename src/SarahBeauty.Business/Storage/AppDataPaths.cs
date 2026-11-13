using System;
using System.IO;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Storage;

public static class AppDataPaths
{
    public static string GetTestFolder()=>BusinessDataFolders.Resolve(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),BusinessMode.Test,Environment.GetEnvironmentVariable("SARAHBEAUTY_TEST_DIRECTORY"));
    public static string GetLiveFolder()=>BusinessDataFolders.Resolve(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),BusinessMode.Live);

    public static string EnsureTestFolderExists()
    {
        string folderPath = GetTestFolder();

        Directory.CreateDirectory(folderPath);
        return folderPath;
    }
}
