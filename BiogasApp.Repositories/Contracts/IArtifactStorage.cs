namespace BiogasApp.Repositories.Contracts;

public interface IArtifactStorage
{
    Task<string> SaveAsync(
        Guid simulationRunId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string fileUrl, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string fileUrl, CancellationToken cancellationToken = default);

    Task<string> ComputeSha256Async(Stream content, CancellationToken cancellationToken = default);
}