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
        var overview = await _dashboards.GetOverviewAsync(projectId, cancellationToken);
        return View(overview);
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