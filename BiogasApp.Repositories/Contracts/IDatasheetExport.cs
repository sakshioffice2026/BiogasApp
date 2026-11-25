using BiogasApp.Model.DTOs;

namespace BiogasApp.Repositories.Contracts;

public interface IDatasheetExport
{
    Task<EquipmentDatasheetSetDto> GetDatasheetsAsync(Guid pidDiagramId, CancellationToken cancellationToken = default);
    Task<byte[]> BuildDatasheetExcelAsync(Guid pidDiagramId, string? tag = null, string? view = null, CancellationToken cancellationToken = default);

    Task<byte[]> BuildDatasheetPdfAsync(Guid pidDiagramId, string? tag = null, string? view = null, CancellationToken cancellationToken = default);

    Task SaveVendorDataAsync(Guid pidDiagramId, EquipmentVendorDataDto data, string? updatedBy, CancellationToken cancellationToken = default);
}
