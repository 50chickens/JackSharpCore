namespace JackSharp.ConsoleApp.Lv2Loader.Logging;

/// <summary>
/// Generic logger interface used for dependency injection.
/// Use ILog&lt;T&gt; to obtain a logger scoped to the consuming type.
/// </summary>
public interface ILog<T>
{
    void Info(string message);
    void Debug(string message);
    void Warn(string message);
    void Error(string message);
    void Error(Exception ex, string message);
    void Trace(string message);
}
