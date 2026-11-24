using System.Text;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>A scratch folder for key files, deleted (best effort) when the test is done.</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "seoreport-tests-" + Guid.NewGuid().ToString("N"));

    public TempDirectory() => Directory.CreateDirectory(Root);

    /// <summary>Writes <paramref name="text"/> (UTF-8 without a BOM unless <paramref name="encoding"/> says otherwise) and returns the path.</summary>
    public string Write(string name, string text, Encoding? encoding = null)
    {
        var path = Path.Combine(Root, name);
        File.WriteAllText(path, text, encoding ?? new UTF8Encoding(false));
        return path;
    }

    public string WriteBytes(string name, byte[] bytes)
    {
        var path = Path.Combine(Root, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A scratch folder left behind in the temp directory is harmless.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
