using System.Reflection;

namespace AVAMMB1.Core.Content;

/// <summary>Supplies raw JSON content files by relative path (e.g. <c>monsters.json</c>, <c>Maps/town.json</c>).</summary>
public interface IContentSource
{
    /// <summary>Human readable description (for diagnostics).</summary>
    string Description { get; }

    /// <summary>Lists available relative paths using forward slashes.</summary>
    IEnumerable<string> List();

    /// <summary>Opens a content file.</summary>
    /// <param name="path">Relative path.</param>
    Stream Open(string path);
}

/// <summary>Reads content embedded in the <c>AVAMMB1.Core</c> assembly (built from <c>/Assets/Data</c>).</summary>
public sealed class EmbeddedContentSource : IContentSource
{
    private const string Prefix = "AVAMMB1.Data/";
    private readonly Assembly _assembly = typeof(EmbeddedContentSource).Assembly;

    /// <inheritdoc />
    public string Description => "embedded content";

    /// <inheritdoc />
    public IEnumerable<string> List() =>
        _assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal))
            .Select(n => n[Prefix.Length..].Replace('\\', '/'));

    /// <inheritdoc />
    public Stream Open(string path)
    {
        var name = _assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.StartsWith(Prefix, StringComparison.Ordinal) &&
                                 n[Prefix.Length..].Replace('\\', '/') == path);
        return name is null
            ? throw new FileNotFoundException($"Embedded content '{path}' not found.")
            : _assembly.GetManifestResourceStream(name)!;
    }
}

/// <summary>Reads content from a directory on disk, allowing modding without recompiling.</summary>
/// <param name="root">Root directory containing the JSON files.</param>
public sealed class DirectoryContentSource(string root) : IContentSource
{
    /// <inheritdoc />
    public string Description => $"directory '{root}'";

    /// <inheritdoc />
    public IEnumerable<string> List() =>
        Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'));

    /// <inheritdoc />
    public Stream Open(string path) => File.OpenRead(Path.Combine(root, path));
}
