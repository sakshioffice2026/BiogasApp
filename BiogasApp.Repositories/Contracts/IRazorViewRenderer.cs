namespace BiogasApp.Repositories.Contracts;

public interface IRazorViewRenderer
{
    Task<string> RenderViewToStringAsync<TModel>(
        string viewName,
        TModel model,
        CancellationToken cancellationToken = default);
}