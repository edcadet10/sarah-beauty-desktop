using System;
using System.IO;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Storage;

public static class AppDataPaths
{
    public static string GetTestFolder()=>Resolve(BusinessMode.Test);
    public static string GetLiveFolder()=>Resolve(BusinessMode.Live);
    private static string Resolve(BusinessMode mode)
    {
        string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),preview=Environment.GetEnvironmentVariable("SARAHBEAUTY_TEST_DIRECTORY")??"";
#if DEBUG
        string review=Environment.GetEnvironmentVariable("SARAHBEAUTY_REVIEW_DIRECTORY")??"";
        if(!string.IsNullOrWhiteSpace(review))return BusinessReviewFolders.Resolve(local,review,mode,preview);
#endif
        return BusinessDataFolders.Resolve(local,mode,preview);
    }

    public static string EnsureTestFolderExists()
    {
        string folderPath = GetTestFolder();

        Directory.CreateDirectory(folderPath);
        return folderPath;
    }
}
