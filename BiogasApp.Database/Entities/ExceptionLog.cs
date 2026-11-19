namespace BiogasApp.Database.Entities;

public class ExceptionLog
{
    public long Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? Source { get; set; }
    public string? RequestPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
