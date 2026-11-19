using BiogasApp.Database;
using BiogasApp.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BiogasApp.Repositories.Repositories;

/// <summary>
/// Writes unhandled/caught exceptions to the exception_logs table.
///
/// Uses its OWN short-lived DbContext (built from the same options) instead of the
/// request's context, so that:
///   * logging never flushes half-failed pending changes from the request context, and
///   * logging still works when the request is inside a transaction that gets rolled back.
///
/// Logging must never throw: any failure here is written to ILogger and swallowed.
/// </summary>
public class ExceptionHandlerRepository
{
    private const int MaxSourceLength = 200;       // matches ExceptionLog.Source HasMaxLength(200)
    private const int MaxRequestPathLength = 500;  // matches ExceptionLog.RequestPath HasMaxLength(500)

    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly ILogger<ExceptionHandlerRepository> _logger;

    public ExceptionHandlerRepository(
        DbContextOptions<ApplicationDbContext> options,
        ILogger<ExceptionHandlerRepository> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <param name="source">Where it happened, e.g. "ProjectController.Index".</param>
    /// <param name="requestPath">Optional HTTP path, e.g. Request.Path.</param>
    public async Task SaveExceptionAsync(
        Exception exception,
        string? source = null,
        string? requestPath = null,
        CancellationToken cancellationToken = default)
    {
        if (exception is null) return;

        try
        {
            var log = new ExceptionLog
            {
                Message = exception.Message,
                StackTrace = exception.ToString(), // full text incl. inner exceptions
                Source = Truncate(source, MaxSourceLength),
                RequestPath = Truncate(requestPath, MaxRequestPathLength),
                CreatedAt = DateTime.UtcNow
            };

            await using var db = new ApplicationDbContext(_options);
            db.ExceptionLogs.Add(log);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception logEx)
        {
            // Never let logging break the caller's error handling.
            _logger.LogError(logEx,
                "Failed to write exception log. Original error from {Source}: {Message}",
                source, exception.Message);
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value
            : value[..maxLength];
}