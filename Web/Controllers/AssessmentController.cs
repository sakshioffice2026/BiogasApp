using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Layout.Geometry;
using BiogasApp.Layout.Models;
using BiogasApp.Layout.Output;
using BiogasApp.Layout.Placement;
using BiogasApp.Layout.Rules;
using BiogasApp.Layout.Sizing;
using BiogasApp.Model.DTOs;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

public class AssessmentController : Controller
{
    private const string ConvertedMessage = "This assessment is already converted to a project and can no longer be edited.";

    private readonly IAssessmentRepository _assessments;
    private readonly IClearanceRuleRepository _rules;
    private readonly IEnquiryRepository _enquiries;

    public AssessmentController(IAssessmentRepository assessments, IClearanceRuleRepository rules, IEnquiryRepository enquiries)
    {
        _assessments = assessments;
        _rules = rules;
        _enquiries = enquiries;
    }

    // ------------------------------------------------------------------ list, create, delete

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await _assessments.GetListAsync(cancellationToken));

    [HttpGet]
    public IActionResult Create() => View(new AssessmentDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AssessmentDto model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Enter an assessment name.");

        if (model.CapacityTpd <= 0)
            ModelState.AddModelError(nameof(model.CapacityTpd), "Enter the plant capacity in tonnes per day.");

        if (!ModelState.IsValid) return View(model);

        model.Id = null;
        model.CreatedBy = User?.Identity?.Name;
        model.Site = new SiteDto { InputMode = "Typed" };

        try
        {
            var saved = await _assessments.SaveAsync(model, cancellationToken);
            return RedirectToAction(nameof(Site), new { id = saved.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _assessments.DeleteAsync(id, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["AssessmentError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------ site check

    [HttpGet]
    public async Task<IActionResult> Site(Guid id, CancellationToken cancellationToken)
    {
        var a = await _assessments.GetAsync(id, cancellationToken);
        if (a == null) return NotFound();

        return View(await BuildViewModelAsync(a, includeLayouts: false, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSite([FromBody] SiteDto dto, CancellationToken cancellationToken)
    {
        if (dto == null || dto.ProjectId == Guid.Empty)
            return BadRequest(new { success = false, message = "Assessment is missing." });

        var a = await _assessments.GetAsync(dto.ProjectId, cancellationToken);
        if (a == null) return NotFound();

        if (IsConverted(a))
            return BadRequest(new { success = false, message = ConvertedMessage });

        a.Site = dto;

        try
        {
            var saved = await _assessments.SaveAsync(a, cancellationToken);

            var poly = saved.Site.PlotCorners.Select(c => new Point2D(c.X, c.Y)).ToList();
            var errors = PlotValidator.Validate(poly);

            return Json(new { success = errors.Count == 0, site = saved.Site, errors });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // ------------------------------------------------------------------ layout editor

    [HttpGet]
    public async Task<IActionResult> Editor(Guid id, CancellationToken cancellationToken)
    {
        var a = await _assessments.GetAsync(id, cancellationToken);
        if (a == null) return NotFound();

        var model = await BuildViewModelAsync(a, includeLayouts: true, cancellationToken);

        if (model.ActiveLayout != null)
            model.ActiveLayout.SvgMarkup = await BuildSvgForSavedAsync(model.Site, model.Equipment, model.ActiveLayout, cancellationToken);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate([FromBody] GenerateLayoutRequest request, CancellationToken cancellationToken)
    {
        var ctx = await LoadContextAsync(request.ProjectId, cancellationToken);
        if (ctx.Error != null) return ctx.Error;

        foreach (var eq in ctx.Equipment.Where(e => !e.HeightM.HasValue || e.HeightM.Value <= 0))
            eq.HeightM = StandardHeightM(eq.Tag);
        var locked = request.LockedItems.Where(i => i.IsLocked)
            .Select(i => new LockedPlacement(i.EquipmentItemId, i.InstanceNo, i.X, i.Y, i.RotationDeg))
            .ToList();

        var input = BuildInput(ctx.Site!, ctx.Equipment, locked);
        var result = new PlacementEngine(ctx.Provider!).Generate(input);

        return Json(ToDto(ctx, input, result, "Auto"));
    }

    private static double? StandardHeightM(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        var prefix = tag.Split('-')[0].Trim().ToUpperInvariant();

        return prefix switch
        {
            "AD" => 12.0,
            "MT" => 4.0,
            "FT" => 5.0,
            "DT" => 6.0,
            "PH" => 4.0,
            "PWT" => 4.0,
            "PWP" => 4.0,
            "SLS" => 6.0,
            "LG" => 3.0,
            "WY" => 2.0,
            "FS" => 3.0,
            "FB" => 3.0,
            "DY" => 1.0,
            "MB" => 5.0,
            "SU" => 6.0,
            "CF" => 5.0,
            "FL" => 12.0,
            "HWG" => 5.0,
            "MCC" => 4.0,
            "TR" => 4.0,
            "SEC" => 3.0,
            "FWT" => 5.0,
            "FWP" => 4.0,
            "WB" => 0.5,
            "ADM" => 4.0,
            "PK" => 0.5,
            _ => null
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Evaluate([FromBody] LayoutDto layout, CancellationToken cancellationToken)
    {
        var ctx = await LoadContextAsync(layout.ProjectId, cancellationToken);
        if (ctx.Error != null) return ctx.Error;

        var input = BuildInput(ctx.Site!, ctx.Equipment, new List<LockedPlacement>());
        var items = ToPlaced(layout.Items, ctx.Equipment);
        var result = new PlacementEngine(ctx.Provider!).Evaluate(input, items);

        return Json(ToDto(ctx, input, result, "Manual"));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVersion([FromBody] LayoutDto layout, CancellationToken cancellationToken)
    {
        var ctx = await LoadContextAsync(layout.ProjectId, cancellationToken);
        if (ctx.Error != null) return ctx.Error;
        if (ctx.Converted) return BadRequest(new { success = false, message = ConvertedMessage });

        layout.SimulationRunId = Guid.Empty;
        layout.RuleSetId = ctx.Provider!.RuleSetId;
        layout.RuleSetVersion = ctx.Provider.RuleSetVersion;
        layout.IsDraft = true;
        layout.CreatedBy = User?.Identity?.Name;

        var saved = await _assessments.SaveLayoutAsync(layout, cancellationToken);
        saved.SvgMarkup = await BuildSvgForSavedAsync(ctx.Site!, ctx.Equipment, saved, cancellationToken);

        return Json(saved);
    }

    [HttpGet]
    public async Task<IActionResult> Version(Guid id, CancellationToken cancellationToken)
    {
        var layout = await _assessments.GetLayoutAsync(id, cancellationToken);
        if (layout == null) return NotFound();

        var a = await _assessments.GetAsync(layout.ProjectId, cancellationToken);
        if (a != null)
            layout.SvgMarkup = await BuildSvgForSavedAsync(a.Site, a.Equipment, layout, cancellationToken);

        return Json(layout);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVersion(Guid id, CancellationToken cancellationToken)
    {
        await _assessments.DeleteLayoutAsync(id, cancellationToken);
        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadDxf(Guid id, CancellationToken cancellationToken)
    {
        var layout = await _assessments.GetLayoutAsync(id, cancellationToken);
        if (layout == null) return NotFound();

        var a = await _assessments.GetAsync(layout.ProjectId, cancellationToken);
        if (a == null) return NotFound();

        var errors = PlotValidator.Validate(a.Site.PlotCorners.Select(c => new Point2D(c.X, c.Y)).ToList());
        if (errors.Count > 0) return BadRequest(string.Join(" ", errors));

        var provider = await _rules.CreateProviderAsync(layout.RuleSetId, cancellationToken);
        var input = BuildInput(a.Site, a.Equipment, new List<LockedPlacement>());
        var items = ToPlaced(layout.Items, a.Equipment);
        var result = new PlacementEngine(provider).Evaluate(input, items);
        var roads = RoadPlanner.Plan(input, ZoneAssigner.Assign(input), provider).Roads;

        int official = result.Warnings.Count(w => w.RuleStatus == RuleStatus.Official);
        int reference = result.Warnings.Count(w => w.RuleStatus == RuleStatus.Reference);
        int assumed = result.Warnings.Count(w => w.RuleStatus == RuleStatus.Assumed);
        var summary = result.Warnings.Count == 0
            ? "No warnings"
            : $"{result.Warnings.Count} warning(s): {official} Official, {reference} Reference, {assumed} Assumed";

        var info = new TitleBlockInfo(
            ProjectName: a.Name,
            Location: $"India ({a.Site.Latitude:0.0000}, {a.Site.Longitude:0.0000})",
            CapacityText: $"{a.CapacityTpd:0.#} TPD CBG",
            DrawingNo: $"GA-{a.Id.GetValueOrDefault().ToString()[..8].ToUpperInvariant()}-V{layout.VersionNo}",
            Revision: $"R{layout.VersionNo}",
            Date: DateTime.UtcNow,
            RuleSetName: $"Clearance rules v{provider.RuleSetVersion}",
            RuleSetVersion: provider.RuleSetVersion,
            LayoutOriginText: string.Equals(layout.Origin, "Manual", StringComparison.OrdinalIgnoreCase)
                ? "Manually edited layout"
                : "Auto-generated layout",
            WarningSummary: summary);

        var bytes = SiteDxfWriter.Write(input, result, roads, info);

        var safe = new string(a.Name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
        if (safe.Length == 0) safe = "Assessment";
        if (safe.Length > 40) safe = safe[..40];

        return File(bytes, "application/dxf", $"GA_{safe}_V{layout.VersionNo}_{DateTime.UtcNow:yyyyMMddHHmm}.dxf");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEquipment([FromBody] EquipmentItemDto item, CancellationToken cancellationToken)
    {
        if (item == null || item.ProjectId == Guid.Empty || string.IsNullOrWhiteSpace(item.Tag) || string.IsNullOrWhiteSpace(item.Name))
            return BadRequest(new { success = false, message = "Tag and name are required." });

        bool circle = string.Equals(item.Shape, "Circle", StringComparison.OrdinalIgnoreCase);
        bool sizeOk = circle ? item.DiameterM > 0 : item.WidthM > 0 && item.LengthM > 0;
        if (!sizeOk)
            return BadRequest(new { success = false, message = "Enter the size of the item." });

        var a = await _assessments.GetAsync(item.ProjectId, cancellationToken);
        if (a == null) return NotFound();
        if (IsConverted(a)) return BadRequest(new { success = false, message = ConvertedMessage });

        if (!item.Id.HasValue) item.IsCustom = true;

        var saved = await _assessments.SaveEquipmentItemAsync(item, cancellationToken);
        return Json(new { success = true, item = saved });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEquipment(Guid id, CancellationToken cancellationToken)
    {
        await _assessments.DeleteEquipmentItemAsync(id, cancellationToken);
        return Json(new { success = true });
    }

    // ------------------------------------------------------------------ proposal

    [HttpGet]
    public async Task<IActionResult> Proposal(Guid id, CancellationToken cancellationToken)
    {
        var a = await _assessments.GetAsync(id, cancellationToken);
        if (a == null) return NotFound();

        if (IsConverted(a))
        {
            TempData["AssessmentError"] = "This assessment is already converted to a project.";
            return RedirectToAction(nameof(Index));
        }

        if (a.EnquiryId.HasValue)
            return RedirectToAction("Create", "EnquiryProposal", new { enquiryId = a.EnquiryId.Value });

        return RedirectToAction(nameof(Customer), new { assessmentId = id });
    }

    [HttpGet]
    public async Task<IActionResult> Customer(Guid assessmentId, CancellationToken cancellationToken)
    {
        var a = await _assessments.GetAsync(assessmentId, cancellationToken);
        if (a == null) return NotFound();

        if (IsConverted(a))
        {
            TempData["AssessmentError"] = "This assessment is already converted to a project.";
            return RedirectToAction(nameof(Index));
        }

        if (a.EnquiryId.HasValue)
            return RedirectToAction("Create", "EnquiryProposal", new { enquiryId = a.EnquiryId.Value });

        ViewBag.AssessmentId = assessmentId;
        ViewBag.AssessmentName = a.Name;

        return View(new EnquiryFormViewModel { Source = EnquirySource.Phone, LocationCountry = "India", EndUse = "CBG" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Customer(Guid assessmentId, EnquiryFormViewModel model, CancellationToken cancellationToken)
    {
        var a = await _assessments.GetAsync(assessmentId, cancellationToken);
        if (a == null) return NotFound();

        if (IsConverted(a))
        {
            TempData["AssessmentError"] = "This assessment is already converted to a project.";
            return RedirectToAction(nameof(Index));
        }

        if (a.EnquiryId.HasValue)
            return RedirectToAction("Create", "EnquiryProposal", new { enquiryId = a.EnquiryId.Value });

        ViewBag.AssessmentId = assessmentId;
        ViewBag.AssessmentName = a.Name;

        if (!ModelState.IsValid) return View(model);

        try
        {
            var enquiry = new Enquiry
            {
                Id = Guid.NewGuid(),
                CustomerName = model.CustomerName,
                CompanyName = model.CompanyName,
                Email = model.Email,
                Phone = model.Phone,
                LocationCity = model.LocationCity,
                LocationState = model.LocationState,
                LocationCountry = model.LocationCountry,
                FeedstockType = model.FeedstockType,
                FeedstockQtyTpd = model.FeedstockQtyTpd,
                EndUse = model.EndUse,
                Message = model.Message,
                Source = EnquirySource.Phone
            };

            var created = await _enquiries.AddAsync(enquiry, cancellationToken);
            await _assessments.LinkEnquiryAsync(assessmentId, created.Id, cancellationToken);

            TempData["Success"] = $"Enquiry {created.EnquiryNo} created and linked to this assessment.";
            return RedirectToAction("Create", "EnquiryProposal", new { enquiryId = created.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    // ------------------------------------------------------------------ convert to project

    [HttpGet]
    public async Task<IActionResult> Convert(Guid id, CancellationToken cancellationToken)
    {
        var a = await _assessments.GetAsync(id, cancellationToken);
        if (a == null) return NotFound();

        if (IsConverted(a) && a.ProjectId.HasValue)
            return RedirectToAction("Detail", "Project", new { id = a.ProjectId.Value });

        if (!await _assessments.HasApprovedProposalAsync(id, cancellationToken))
        {
            TempData["AssessmentError"] = "Convert is available only after the proposal is Approved.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.AssessmentName = a.Name;
        return View(new ConvertAssessmentDto { AssessmentId = id, ProjectName = a.Name });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(ConvertAssessmentDto model, CancellationToken cancellationToken)
    {
        var a = await _assessments.GetAsync(model.AssessmentId, cancellationToken);
        if (a == null) return NotFound();

        ViewBag.AssessmentName = a.Name;

        if (string.IsNullOrWhiteSpace(model.ProjectName))
        {
            ModelState.AddModelError(nameof(model.ProjectName), "Enter a project name.");
            return View(model);
        }

        try
        {
            var projectId = await _assessments.ConvertToProjectAsync(model, cancellationToken);
            return RedirectToAction("Detail", "Project", new { id = projectId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    // ------------------------------------------------------------------ helpers

    private static bool IsConverted(AssessmentDto a) =>
        string.Equals(a.Status, "Converted", StringComparison.OrdinalIgnoreCase);

    private async Task<LayoutEditorViewModel> BuildViewModelAsync(
        AssessmentDto a,
        bool includeLayouts,
        CancellationToken cancellationToken)
    {
        var ruleSet = await _rules.GetActiveRuleSetAsync(cancellationToken);
        var id = a.Id ?? Guid.Empty;

        var model = new LayoutEditorViewModel
        {
            ProjectId = id,
            ProjectName = a.Name,
            SimulationRunId = Guid.Empty,
            Site = a.Site,
            Equipment = a.Equipment,
            RuleSetId = ruleSet?.Id,
            RuleSetName = ruleSet?.Name ?? string.Empty,
            RuleSetVersion = ruleSet?.Version ?? 0,
            ProposalApproved = !IsConverted(a) && await _assessments.HasApprovedProposalAsync(id, cancellationToken)
        };

        if (includeLayouts)
        {
            model.ActiveLayout = await _assessments.GetLatestLayoutAsync(id, cancellationToken);
            model.Versions = await _assessments.GetLayoutVersionsAsync(id, cancellationToken);
        }

        return model;
    }

    private sealed class Context
    {
        public IActionResult? Error { get; set; }
        public SiteDto? Site { get; set; }
        public List<EquipmentItemDto> Equipment { get; set; } = new();
        public IClearanceRuleProvider? Provider { get; set; }
        public bool Converted { get; set; }
    }

    private async Task<Context> LoadContextAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        var ctx = new Context();

        var a = await _assessments.GetAsync(assessmentId, cancellationToken);
        if (a == null)
        {
            ctx.Error = NotFound();
            return ctx;
        }

        ctx.Converted = IsConverted(a);
        ctx.Site = a.Site;

        var plot = ctx.Site.PlotCorners.Select(c => new Point2D(c.X, c.Y)).ToList();
        var errors = PlotValidator.Validate(plot);
        if (errors.Count > 0)
        {
            ctx.Error = BadRequest(new { success = false, message = string.Join(" ", errors) });
            return ctx;
        }

        ctx.Equipment = a.Equipment;
        ctx.Provider = await _rules.CreateProviderAsync(null, cancellationToken);
        return ctx;
    }

    private static LayoutInput BuildInput(SiteDto site, List<EquipmentItemDto> equipment, List<LockedPlacement> locked)
    {
        var geo = new GeoProjection(site.Latitude, site.Longitude);

        var entities = equipment.Select(e => new ProjectEquipmentItem
        {
            Id = e.Id ?? Guid.Empty,
            ProjectId = e.ProjectId,
            Tag = e.Tag,
            Name = e.Name,
            HazardClass = ParseEnum(e.HazardClass, HazardClass.Other),
            Zone = ParseEnum(e.Zone, EquipmentZone.Process),
            Shape = ParseEnum(e.Shape, EquipmentShape.Rectangle),
            WidthM = (decimal?)e.WidthM,
            LengthM = (decimal?)e.LengthM,
            DiameterM = (decimal?)e.DiameterM,
            HeightM = (decimal?)(e.HeightM ?? StandardHeightM(e.Tag)),
            Quantity = e.Quantity,
            IsEnabled = e.IsEnabled,
            AllowRotation = e.AllowRotation,
            NeedsRoadAccess = e.NeedsRoadAccess
        });

        return new LayoutInput
        {
            PlotCorners = site.PlotCorners.Select(c => new Point2D(c.X, c.Y)).ToList(),
            GateEdgeIndex = site.GateEdgeIndex,
            GateOffsetM = site.GateOffsetM,
            NorthAngleDeg = site.NorthAngleDeg,
            WindFromDeg = site.WindFromDeg,
            SlopeDirectionDeg = site.SlopeDirectionDeg,
            PowerSideDeg = site.PowerSideDeg,
            GasOfftakeSideDeg = site.GasOfftakeSideDeg,
            Equipment = FootprintBuilder.FromItems(entities),
            NoBuildZones = site.NoBuildZones
                .Where(z => z.Polygon.Count >= 3)
                .Select(z => new NoBuildInput(
                    ParseEnum(z.Reason, NoBuildReason.Other),
                    z.Polygon.Select(p => new Point2D(p.X, p.Y)).ToList(),
                    z.Notes))
                .ToList(),
            Receptors = site.Receptors
                .Where(r => r.IsConfirmed)
                .Select(r => new ReceptorInput(
                    ParseEnum(r.Type, ReceptorType.Other),
                    r.Name,
                    geo.ToMetres(r.Latitude, r.Longitude),
                    r.DistanceFromPlotM))
                .ToList(),
            LockedItems = locked
        };
    }

    private static List<PlacedItem> ToPlaced(IEnumerable<LayoutItemDto> items, List<EquipmentItemDto> equipment)
    {
        var byId = equipment.Where(e => e.Id.HasValue).ToDictionary(e => e.Id!.Value);

        return items.Select(i =>
        {
            byId.TryGetValue(i.EquipmentItemId, out var e);
            return new PlacedItem
            {
                EquipmentId = i.EquipmentItemId,
                InstanceNo = i.InstanceNo,
                Tag = i.Tag,
                Name = i.Name,
                HazardClass = ParseEnum(e?.HazardClass, HazardClass.Other),
                Zone = ParseEnum(i.Zone, EquipmentZone.Process),
                Shape = ParseEnum(i.Shape, EquipmentShape.Rectangle),
                X = i.X,
                Y = i.Y,
                WidthM = i.WidthM,
                LengthM = i.LengthM,
                RotationDeg = i.RotationDeg,
                IsLocked = i.IsLocked,
                NeedsRoadAccess = e?.NeedsRoadAccess ?? true
            };
        }).ToList();
    }

    private static LayoutDto ToDto(Context ctx, LayoutInput input, LayoutResult result, string origin)
    {
        var roads = RoadPlanner.Plan(input, ZoneAssigner.Assign(input), ctx.Provider).Roads;

        return new LayoutDto
        {
            ProjectId = ctx.Site!.ProjectId,
            SimulationRunId = Guid.Empty,
            Origin = origin,
            RuleSetId = ctx.Provider!.RuleSetId,
            RuleSetVersion = ctx.Provider.RuleSetVersion,
            IsDraft = true,
            GatePoint = new PointDto { X = result.GatePoint.X, Y = result.GatePoint.Y },
            CreatedAt = DateTime.UtcNow,
            Items = result.Items.Select(i => new LayoutItemDto
            {
                EquipmentItemId = i.EquipmentId,
                InstanceNo = i.InstanceNo,
                Tag = i.Tag,
                Name = i.Name,
                Zone = i.Zone.ToString(),
                Shape = i.Shape.ToString(),
                X = i.X,
                Y = i.Y,
                RotationDeg = i.RotationDeg,
                WidthM = i.WidthM,
                LengthM = i.LengthM,
                IsLocked = i.IsLocked
            }).ToList(),
            Warnings = result.Warnings.Select(w => new LayoutWarningDto
            {
                Code = w.Code,
                Message = w.Message,
                RuleStatus = w.RuleStatus?.ToString(),
                SourceNote = w.SourceNote,
                EquipmentItemId = w.EquipmentId,
                InstanceNo = w.InstanceNo,
                OtherEquipmentItemId = w.OtherEquipmentId,
                ActualDistanceM = w.ActualDistanceM,
                RequiredDistanceM = w.RequiredDistanceM
            }).ToList(),
            Roads = roads.Select(r => new LayoutRoadDto
            {
                Kind = r.Kind.ToString(),
                WidthM = r.WidthM,
                Points = r.Points.Select(p => new PointDto { X = p.X, Y = p.Y }).ToList()
            }).ToList(),
            SvgMarkup = SvgPreviewBuilder.Build(input, result, roads)
        };
    }

    private async Task<string?> BuildSvgForSavedAsync(
        SiteDto site,
        List<EquipmentItemDto> equipment,
        LayoutDto layout,
        CancellationToken cancellationToken)
    {
        if (site.PlotCorners.Count < 3) return null;

        var provider = await _rules.CreateProviderAsync(layout.RuleSetId, cancellationToken);
        var input = BuildInput(site, equipment, new List<LockedPlacement>());
        var items = ToPlaced(layout.Items, equipment);
        var result = new PlacementEngine(provider).Evaluate(input, items);
        var roads = RoadPlanner.Plan(input, ZoneAssigner.Assign(input), provider).Roads;

        return SvgPreviewBuilder.Build(input, result, roads);
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var result) ? result : fallback;
}