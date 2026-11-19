using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using BiogasApp.Repositories.Contracts;

namespace BiogasApp.Repositories.Repositories;

/// <summary>
/// Renders a Razor view to an HTML string (for proposal pages, e-mails and so on).
///
/// Requires, in BiogasApp.Repositories.csproj:
///   &lt;ItemGroup&gt;&lt;FrameworkReference Include="Microsoft.AspNetCore.App" /&gt;&lt;/ItemGroup&gt;
///
/// viewName may be an absolute path ("~/Views/Proposal/Summary.cshtml") or a view name found in
/// Views/Shared ("ProposalSummary").
/// </summary>
public class RazorViewRenderRepository : IRazorViewRenderer
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _serviceProvider;

    public RazorViewRenderRepository(
        IRazorViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IServiceProvider serviceProvider)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> RenderViewToStringAsync<TModel>(
        string viewName,
        TModel model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var httpContext = new DefaultHttpContext { RequestServices = _serviceProvider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        var isPath = viewName.StartsWith("~/", StringComparison.Ordinal) ||
                     viewName.StartsWith("/", StringComparison.Ordinal) ||
                     viewName.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase);

        var viewResult = isPath
            ? _viewEngine.GetView(executingFilePath: null, viewPath: viewName, isMainPage: true)
            : _viewEngine.FindView(actionContext, viewName, isMainPage: false);

        if (!viewResult.Success)
        {
            var searched = string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>());
            throw new InvalidOperationException($"View '{viewName}' was not found. Searched: {searched}");
        }

        var viewData = new ViewDataDictionary<TModel>(
            new Microsoft.AspNetCore.Mvc.ModelBinding.Metadata.EmptyModelMetadataProvider(),
            new ModelStateDictionary())
        {
            Model = model
        };

        await using var writer = new StringWriter();

        var viewContext = new ViewContext(
            actionContext,
            viewResult.View,
            viewData,
            new TempDataDictionary(httpContext, _tempDataProvider),
            writer,
            new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}