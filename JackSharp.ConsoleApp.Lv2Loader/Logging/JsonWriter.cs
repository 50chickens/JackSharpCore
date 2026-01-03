using System.Text.Json;
using System.Text.Json.Serialization;

namespace JackSharp.ConsoleApp.Lv2Loader.Logging;

/// <summary>
/// Writes results to a file in JSON format.
/// </summary>
public class JsonWriter(string path)
{
    private readonly string _path = path ?? throw new ArgumentNullException("Path cannot be null");
    private readonly object _lock = new object();

    public void Append<T>(T obj)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };
        var json = JsonSerializer.Serialize(obj, options);
        lock (_lock)
        {
            using var fs = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var sw = new StreamWriter(fs);
            sw.WriteLine(json);
            sw.Flush();
            fs.Flush(true);
        }
    }
}
