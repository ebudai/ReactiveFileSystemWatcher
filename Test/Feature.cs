using Budaisoft.FileSystem;
using System.Reactive.Linq;

namespace Test;

public class Feature
{
    [Fact]
    public async Task IgnoreFolder()
    {
        const string basefolder = nameof(IgnoreFolder);
        const string subfolder = nameof(subfolder);
        const string filename = "change.file";

        int changes = 0;
        int other = 0;

        DeleteBaseFolder(basefolder);
        
        ReactiveFileSystemWatcher watcher = null;

        try
        {
            var parent = Directory.CreateDirectory(basefolder);
            var child = parent.CreateSubdirectory(subfolder);
            
            FileInfo parentFile = new(Path.Combine(parent.FullName, filename));
            await File.WriteAllTextAsync(parentFile.FullName, "first");

            FileInfo childFile = new(Path.Combine(child.FullName, filename));
            await File.WriteAllTextAsync(childFile.FullName, "first");

            watcher = new(root: basefolder, ignoredFolders: new[] { subfolder });
            var events = watcher.SelectMany(_ => _);
            events.Where(change => change.ChangeType is FileSystemChange.ChangeTypes.Change).Subscribe(_ => changes++);
            events.Where(change => change.ChangeType is not FileSystemChange.ChangeTypes.Change).Subscribe(_ => other++);

            await Task.Delay(WAIT);

            await File.WriteAllTextAsync(parentFile.FullName, "second");
            await File.WriteAllTextAsync(childFile.FullName, "second");

            await Task.Delay(WAIT);
        }
        finally
        {
            watcher?.Dispose();
            DeleteBaseFolder(basefolder);
        }

        Assert.Equal(1, changes);
        Assert.Equal(0, other);
    }
}
