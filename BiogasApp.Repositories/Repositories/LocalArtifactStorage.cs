using System.Security.Cryptography;
using BiogasApp.Repositories.Contracts;

namespace BiogasApp.Repositories.Repositories;

/// <summary>
/// Stores artifacts on local disk under {root}/{simulationRunId:N}/{fileName}.
/// The returned "file url" is the path relative to the root, using forward slashes.
/// Files are never overwritten: a name clash gets a numeric suffix (name_1.dxf, name_2.dxf ...).
///
/// Register as a singleton (stateless):
///   services.AddSingleton&lt;IArtifactStorage&gt;(_ =>
///       new LocalArtifactStorage(Path.Combine(env.ContentRootPath, "App_Data", "artifacts")));
/// </summary>
public class LocalArtifactStorage : IArtifactStorage
{
    private const int MaxFileNameLength = 150;
    private const int MaxCollisionAttempts = 1000;

    private readonly string _root;

    public LocalArtifactStorage(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Artifact storage root path is required.", nameof(rootPath));

        _root = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(
        Guid simulationRunId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var safeName = SanitizeFileName(fileName);
        var directory = Path.Combine(_root, simulationRunId.ToString("N"));
        Directory.CreateDirectory(directory);

        var baseName = Path.GetFileNameWithoutExtension(safeName);
        var extension = Path.GetExtension(safeName);

        for (var attempt = 0; attempt < MaxCollisionAttempts; attempt++)
        {
            var candidate = attempt == 0 ? safeName : $"{baseName}_{attempt}{extension}";
            var fullPath = Path.Combine(directory, candidate);

            try
            {
                await using var target = new FileStream(
                    fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 81920, useAsync: true);

                await content.CopyToAsync(target, cancellationToken);
                return Path.GetRelativePath(_root, fullPath).Replace('\\', '/');
            }
            catch (IOException) when (File.Exists(fullPath))
            {
                // Name already taken (possibly by a concurrent save): try the next suffix.
            }
        }

        throw new IOException($"Could not find a free file name for '{safeName}'.");
    }

    public Task<Stream?> OpenReadAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(fileUrl);

        if (path is null || !File.Exists(path))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(fileUrl);

        if (path is null || !File.Exists(path))
            return Task.FromResult(false);

        File.Delete(path);
        return Task.FromResult(true);
    }

    /// <summary>Lower-case hex SHA-256. A seekable stream is read from the start and its position restored.</summary>
    public async Task<string> ComputeSha256Async(Stream content, CancellationToken cancellationToken = default)
    {
        long? originalPosition = null;

        if (content.CanSeek)
        {
            originalPosition = content.Position;
            content.Position = 0;
        }

        var hash = await SHA256.HashDataAsync(content, cancellationToken);

        if (originalPosition.HasValue)
            content.Position = originalPosition.Value;

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Maps a stored relative url to a full path; returns null if it would escape the root.</summary>
    private string? ResolvePath(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return null;

        var full = Path.GetFullPath(Path.Combine(_root, fileUrl.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;

        return full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    private static string SanitizeFileName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Trim());

        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        name = name.Trim('.', ' ');

        if (name.Length == 0)
            name = "file";

        if (name.Length > MaxFileNameLength)
        {
            var extension = Path.GetExtension(name);
            var stem = Path.GetFileNameWithoutExtension(name);
            var keep = Math.Max(1, MaxFileNameLength - extension.Length);
            name = stem[..Math.Min(stem.Length, keep)] + extension;
        }

        return name;
    }
}