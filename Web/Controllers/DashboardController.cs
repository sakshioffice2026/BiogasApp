using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

public class DashboardController : Controller
{
    private readonly IDashboardRepository _dashboards;

    public DashboardController(IDashboardRepository dashboards)
    {
        _dashboards = dashboards;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? projectId, CancellationToken cancellationToken)
    {
        ViewData["StatusCounts"] = await _dashboards.GetProjectStatusCountsAsync(cancellationToken);
        ViewData["TotalRuns"] = await _dashboards.GetTotalRunCountAsync(cancellationToken);
        ViewData["ProjectId"] = projectId;

        var latest = projectId.HasValue
            ? await _dashboards.GetLatestDashboardAsync(projectId.Value, cancellationToken)
            : null;

        return View(latest);
    }

    [HttpGet]
    public async Task<IActionResult> Run(Guid runId, CancellationToken cancellationToken)
    {
        var dashboard = await _dashboards.GetDashboardAsync(runId, cancellationToken);
        if (dashboard == null)
        {
            return NotFound();
        }

        return Json(dashboard);
    }
}