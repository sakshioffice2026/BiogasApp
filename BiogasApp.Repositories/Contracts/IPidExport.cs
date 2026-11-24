namespace BiogasApp.Repositories.Contracts;

public interface IPidExport
{
    Task<byte[]> BuildListsExcelAsync(Guid pidDiagramId, CancellationToken cancellationToken = default);
}
