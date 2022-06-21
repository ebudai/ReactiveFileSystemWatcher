using Budaisoft.FileSystem;

namespace Test;

public class Integration
{
    private static void DeleteBaseFolder(string folder)
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

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
            InduceError(PATH);

            await Task.Delay(500);

            Assert.True(errors > 0);

            errors = 0;
            Directory.CreateDirectory(PATH);

            watcher.Error -= HandleError;
            watcher.Start();
            InduceError(PATH);

            await Task.Delay(500);

            Assert.Equal(0, errors);
        }        
        finally
        {
            DeleteBaseFolder(PATH);
        }
        
        void HandleError(object sender, ErrorEventArgs e) => errors++;
        static void InduceError(string path) => DeleteBaseFolder(path);
    }
}
