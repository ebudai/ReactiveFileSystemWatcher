using Budaisoft.FileSystem;
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
}
