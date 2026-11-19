using BiogasApp.Model.DTOs;

namespace BiogasApp.Repositories.Contracts;

public interface IProposalExport
{
    Task<ProposalResponseDto> ExportProposalAsync(
        ProposalRequestDto request,
        CancellationToken cancellationToken = default);

    Task<byte[]> BuildBomExcelAsync(
        Guid simulationRunId,
        IReadOnlyDictionary<string, decimal>? rateOverrides = null,
        CancellationToken cancellationToken = default);

    Task<byte[]> BuildProposalPdfAsync(
        Guid simulationRunId,
        FinancialMetricsDto metrics,
        CancellationToken cancellationToken = default);
}