using Budaisoft.FileSystem;

namespace Test.Common;

internal class Common
{
    public static readonly int WAIT = (int)ReactiveFileSystemWatcher.Latency.TotalMilliseconds * 2;

    public static void DeleteBaseFolder(string folder)
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }
}
