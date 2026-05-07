using System.IO;

namespace Launcher;

public sealed class FileLog
{
    private readonly string _path;
    private readonly object _lock = new();

    public FileLog(string path)
    {
        _path = path;
    }

    public void Write(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
        lock (_lock)
        {
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(_path, line);
            }
            catch
            {
            }
        }
    }
}
