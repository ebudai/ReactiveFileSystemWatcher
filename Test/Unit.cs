using Budaisoft.FileSystem;
using System.Diagnostics;
using System.Reactive.Linq;

namespace Test;

public class Unit
{
    private const int WAIT = ReactiveFileSystemWatcher.DEFAULT_TEMPORAL_RESOLUTION_MILLIS * 2;

    private static void DeleteBaseFolder(string folder)
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public async Task AddFile()
    {
        const string basefolder = "AddFile";
        const string filename = "Add.file";
        FileInfo destination = new(Path.Combine(basefolder, filename));
        
        DeleteBaseFolder(basefolder);
        var folder = Directory.CreateDirectory(basefolder);
        FileInfo tempfile = null;

        using ReactiveFileSystemWatcher watcher = new(root: basefolder);

        try
        {
            tempfile = new(Path.Combine(Path.GetTempPath(), filename));
            {
                using var stream = File.Create(tempfile.FullName);
            }
            File.WriteAllText(tempfile.FullName, "test");

            int adds = 0;
            int others = 0;
            var events = watcher.SelectMany(_ => _);
            events.Where(change => change.ChangeType is FileSystemChange.ChangeTypes.Add).Subscribe(_ => adds++);
            events.Where(change => change.ChangeType is not FileSystemChange.ChangeTypes.Add).Subscribe(_ => others++);

            File.Move(tempfile.FullName, destination.FullName);

            await Task.Delay(WAIT);

            Assert.Equal(1, adds);
            Assert.Equal(0, others);
        }
        finally
        {
            tempfile?.Delete();
            DeleteBaseFolder(basefolder);            
        }
    }

    [Fact]
    public async Task AddFolder()
    {
        const string basefolder = "AddFolder";
        const string filename = "added.file";
        DeleteBaseFolder(basefolder);
        Directory.CreateDirectory(basefolder);

        DirectoryInfo tempfolder = null;
        DirectoryInfo destination = null;

        using ReactiveFileSystemWatcher watcher = new(root: basefolder);

        try
        {
            tempfolder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), basefolder));

            FileInfo tempFile = new(Path.Combine(tempfolder.FullName, filename));
            File.WriteAllText(tempFile.FullName, "testtest");

            destination = new(basefolder);

            int adds = 0;
            int others = 0;

            var events = watcher.SelectMany(_ => _);
            events.Where(change => change.ChangeType is FileSystemChange.ChangeTypes.Add).Subscribe(_ => adds++);
            events.Where(change => change.ChangeType is not FileSystemChange.ChangeTypes.Add).Subscribe(_ => others++);

            Process process = OperatingSystem.IsWindows()
                ? new() { StartInfo = new("xcopy.exe", $"{tempfolder.FullName} {destination.FullName} /Y /E /I") }
                : new() { StartInfo = new() { UseShellExecute = true, FileName = "/bin/bash", Arguments = $"cp -a {tempfolder.FullName}/ {destination.Parent.FullName}/" } };

            process.Start();
            await process.WaitForExitAsync();

            await Task.Delay(WAIT);

            Assert.Equal(1, adds);
            Assert.Equal(0, others);
        }
        finally
        {
            Debug.WriteLine("cleaning up");
            tempfolder?.Delete(recursive: true);
            DeleteBaseFolder(basefolder);
            var processes = Process.GetProcessesByName("xcopy");
            foreach (var process in processes) process.Kill();
        }
    }

    [Fact]
    public async Task RenameFile()
    {
        const string basefolder = "RenameFile";
        const string destinationName = "Renamed.file";
        const string filename = "Rename.file";

        int renames = 0;
        int others = 0;

        DeleteBaseFolder(basefolder);
        Directory.CreateDirectory(basefolder);

        try
        {
            DirectoryInfo destination = new(basefolder);
            FileInfo renamed = new(Path.Combine(basefolder, filename));
            {
                using var _ = File.Create(renamed.FullName);
            }

            using ReactiveFileSystemWatcher watcher = new (root: basefolder);

            var events = watcher.SelectMany(_ => _);
            events.Where(change => change.ChangeType is FileSystemChange.ChangeTypes.Rename).Subscribe(_ => renames++);
            events.Where(change => change.ChangeType is not FileSystemChange.ChangeTypes.Rename).Subscribe(_ => others++);

            File.Move(renamed.FullName, Path.Combine(basefolder, destinationName));

            await Task.Delay(WAIT);

            Assert.Equal(1, renames);
            Assert.Equal(0, others);
        }
        finally
        {
            DeleteBaseFolder(basefolder);
        }
    }

    [Fact]
    public async Task RenameFolder()
    {
        const string basefolder = "RenameFolder";
        const string foldername_before = "RenameFolderBefore";
        const string foldername_after = "RenameFolderAfter";
        const string filename = "Renamed.file";

        int renames = 0;
        int others = 0;

        DirectoryInfo beforefolder = null;
        DirectoryInfo afterfolder = null;
        DeleteBaseFolder(basefolder);        
        
        try
        {
            Directory.CreateDirectory(basefolder);

            beforefolder = Directory.CreateDirectory(Path.Combine(basefolder, foldername_before));
            afterfolder = new(Path.Combine(basefolder, foldername_after));

            FileInfo otherFile = new(Path.Combine(beforefolder.FullName, filename));
            {
                using var _ = File.Create(otherFile.FullName);
            }
            File.WriteAllText(otherFile.FullName, "testtest");

            using ReactiveFileSystemWatcher watcher = new(root: basefolder);

            var events = watcher.SelectMany(_ => _);
            events.Where(change => change.ChangeType is FileSystemChange.ChangeTypes.Rename).Subscribe(_ => renames++);
            events.Where(change => change.ChangeType is not FileSystemChange.ChangeTypes.Rename).Subscribe(_ => others++);

            Directory.Move(beforefolder.FullName, afterfolder.FullName);

            await Task.Delay(WAIT);

            Assert.Equal(1, renames);
            Assert.Equal(0, others);
        }
        finally
        {
            DeleteBaseFolder(basefolder);
        }
    }

    [Fact]
    public async Task ChangeFile()
    {
        const string basefolder = "ChangeFile";
        const string filename = "Change.file";
        
        int changes = 0;
        int others = 0;

        DeleteBaseFolder(basefolder);

        try
        {
            Directory.CreateDirectory(basefolder);

            FileInfo changed = new(Path.Combine(basefolder, filename));
            await File.WriteAllTextAsync(changed.FullName, "first");

            using ReactiveFileSystemWatcher watcher = new(root: basefolder);

            var events = watcher.SelectMany(_ => _);
            events.Where(change => change.ChangeType is FileSystemChange.ChangeTypes.Change).Subscribe(_ => changes++);
            events.Where(change => change.ChangeType is not FileSystemChange.ChangeTypes.Change).Subscribe(_ => others++);

            await Task.Delay(WAIT);  //ensure change latency exceeds temporal resolution of the watcher

            await File.WriteAllTextAsync(changed.FullName, "second");

            await Task.Delay(WAIT);

            Assert.Equal(1, changes);
            Assert.Equal(0, others);
        }
        finally
        {
            DeleteBaseFolder(basefolder);
        }        
    }

    [Fact]
    public async Task DeleteFile()
    {
        const string basefolder = "DeleteFile";
        const string filename = "Delete.file";

        int deletes = 0;
        int others = 0;

        DeleteBaseFolder(basefolder);
        
        try
        {
            Directory.CreateDirectory(basefolder);

            FileInfo deleted = new(Path.Combine(basefolder, filename));
            await File.WriteAllTextAsync(deleted.FullName, "first");

            using ReactiveFileSystemWatcher watcher = new(root: basefolder);

            var events = watcher.SelectMany(_ => _);
            events.Where(change => change.ChangeType is FileSystemChange.ChangeTypes.Delete).Subscribe(_ => deletes++);
            events.Where(change => change.ChangeType is not FileSystemChange.ChangeTypes.Delete).Subscribe(_ => others++);

            deleted?.Delete();

            await Task.Delay(WAIT);

            Assert.Equal(1, deletes);
            Assert.Equal(0, others);
        }
        finally
        {
            DeleteBaseFolder(basefolder);
        }
    }

    [Fact]
    public async Task DeleteFolder()
    {
        const string basefolder = "DeleteFolder";
        const string subfoldername = "FolderToDelete";
        const string filename = "Delete.file";

        int deletes = 0;
        int others = 0;        
        
        DeleteBaseFolder(basefolder);
        
        try
        {
            var folder = Directory.CreateDirectory(basefolder);
            var subfolder = folder.CreateSubdirectory(subfoldername);

            FileInfo deleted = new(Path.Combine(basefolder, subfoldername, filename));
            Debug.WriteLine($"Writing to file {deleted.Name}");
            await File.WriteAllTextAsync(deleted.FullName, "first");

            using ReactiveFileSystemWatcher watcher = new(root: basefolder);

            var events = watcher.SelectMany(_ => _);
            events.Where(change => change.ChangeType is FileSystemChange.ChangeTypes.Delete).Subscribe(_ => deletes++);
            events.Where(change => change.ChangeType is not FileSystemChange.ChangeTypes.Delete).Subscribe(_ => others++);

            subfolder?.Delete(recursive: true);

            await Task.Delay(WAIT);

            Assert.Equal(2, deletes);
            Assert.Equal(0, others);
        }
        finally
        {
            DeleteBaseFolder(basefolder);
        }
    }
}