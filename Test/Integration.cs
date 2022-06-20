using Budaisoft.FileSystem;

namespace Test;

public class Integration
{
    [Fact]
    public void ErrorHandling()
    {
        Assert.False(OperatingSystem.IsWindows());
        
        //const string PATH = nameof(ErrorHandling);

        try
        {

        }
        finally
        { 

        }
        //using ReactiveFileSystemWatcher watcher = new(PATH);
    }

    private static void InduceError(string path)
    {
        for (int i = 0; i < 10000000; i++)
        {
            var name = Path.Combine(path, i.ToString());
            File.Create(name);
            File.Delete(name);
        }
    }
}
