using Budaisoft.FileSystem;
using System.Reactive.Linq;
using System.Reflection;

namespace Test;

public class Integration
{
    [Fact]
    public async Task CustomErrorHandling()
    {
        const string PATH = nameof(CustomErrorHandling);

        int errors = 0;

        DeleteBaseFolder(PATH);
        Directory.CreateDirectory(PATH);

        try
        {
            using ReactiveFileSystemWatcher watcher = new(PATH);

            watcher.Error += HandleError;
            InduceError(watcher);

            await Task.Delay(500);

            Assert.True(errors > 0);

            errors = 0;
            Directory.CreateDirectory(PATH);

            watcher.Error -= HandleError;
            InduceError(watcher);

            await Task.Delay(500);

            Assert.Equal(0, errors);
        }        
        finally
        {
            DeleteBaseFolder(PATH);
        }
        
        void HandleError(object sender, ErrorEventArgs e) => errors++;
        static void InduceError(ReactiveFileSystemWatcher watcher)
        {
            var innerWatcher = typeof(ReactiveFileSystemWatcher).GetField("_watcher", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(watcher);
            var method = typeof(FileSystemWatcher).GetMethod("OnError", BindingFlags.Instance | BindingFlags.NonPublic);
            ErrorEventArgs args = new(new Exception("test exception"));
            method.Invoke(innerWatcher, new object[] { args });
        }
    }

    [Fact]
    public async Task StartStop()
    {
        const string basefolder = nameof(StartStop);
        const string filename = "Add.file";
        const string secondfilename = "Second.file";

        FileInfo destination = new(Path.Combine(basefolder, filename));
        FileInfo seconddestination = new(Path.Combine(basefolder, secondfilename));

        DeleteBaseFolder(basefolder);
        var folder = Directory.CreateDirectory(basefolder);
        FileInfo tempfile = null;
        FileInfo othertempfile = null;

        using ReactiveFileSystemWatcher watcher = new(root: basefolder);

        try
        {
            tempfile = new(Path.Combine(Path.GetTempPath(), filename));
            othertempfile = new(Path.Combine(Path.GetTempPath(), secondfilename));
            {
                using var stream = File.Create(tempfile.FullName);
                using var otherstream = File.Create(othertempfile.FullName);
            }

            File.WriteAllText(tempfile.FullName, "test");
            File.WriteAllText(othertempfile.FullName, "test");

            int adds = 0;
            int others = 0;
            var events = watcher.SelectMany(_ => _);
            events.Where(change => change.ChangeType is FileSystemChange.ChangeTypes.Add).Subscribe(_ => adds++);
            events.Where(change => change.ChangeType is not FileSystemChange.ChangeTypes.Add).Subscribe(_ => others++);

            File.Move(tempfile.FullName, destination.FullName);

            await Task.Delay(WAIT);

            watcher.Stop();

            File.Move(othertempfile.FullName, seconddestination.FullName);  //this add should not be counted

            watcher.Start();

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
}
