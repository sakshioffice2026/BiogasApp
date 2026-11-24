using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;
using BiogasApp.Model.ViewModel;

namespace BiogasApp.Repositories.Contracts;

public interface IPidRepository
{
    Task<string?> GetProjectNameAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<List<PidRunOptionDto>> GetRunOptionsAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<List<ProjectEquipmentItem>> GetProjectEquipmentAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<List<SizedEquipment>> GetSizedEquipmentAsync(Guid simulationRunId, CancellationToken cancellationToken = default);

    Task<List<PidDiagramDto>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<PidDiagramDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PidDiagramDto?> GetLatestAsync(Guid projectId, Guid simulationRunId, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(PidDiagram diagram, string? user, CancellationToken cancellationToken = default);

    Task<PidDiagramDto?> SaveAsync(PidDiagramDto dto, string? user, CancellationToken cancellationToken = default);

    Task<bool> ChangeStatusAsync(Guid id, PidApprovalStatus status, string? user, string? remarks, CancellationToken cancellationToken = default);

    Task<bool> CreateRevisionAsync(Guid id, string? user, string? remarks, CancellationToken cancellationToken = default);

    Task<List<PidRevisionItemDto>> GetRevisionsAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
