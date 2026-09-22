using System;
using System.IO;

namespace SarahBeauty_Desktop.Storage;

public static class AppDataPaths
{
    public static string GetTestFolder()
    {
        string localData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
        {
            throw new InvalidOperationException(
                "Windows could not locate the local application data folder.");
        }

        return Path.Combine(localData, "SarahBeautyDesktop", "Test");
    }

    public static string EnsureTestFolderExists()
    {
        string folderPath = GetTestFolder();

        Directory.CreateDirectory(folderPath);
        return folderPath;
    }
}