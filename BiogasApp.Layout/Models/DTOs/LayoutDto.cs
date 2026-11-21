namespace BiogasApp.Model.DTOs;

public class LayoutItemDto
{
    public Guid EquipmentItemId { get; set; }
    public int InstanceNo { get; set; } = 1;
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public string Shape { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double RotationDeg { get; set; }
    public double WidthM { get; set; }
    public double LengthM { get; set; }
    public bool IsLocked { get; set; }
}

public class LayoutWarningDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    // Official, Reference or Assumed. Null when the warning is not about a rule.
    public string? RuleStatus { get; set; }

    public string? SourceNote { get; set; }
    public Guid? EquipmentItemId { get; set; }
    public int? InstanceNo { get; set; }
    public Guid? OtherEquipmentItemId { get; set; }
    public double? ActualDistanceM { get; set; }
    public double? RequiredDistanceM { get; set; }
}

public class LayoutRoadDto
{
    // Heavy, Main, Loop or FireAccess
    public string Kind { get; set; } = "Main";
    public double WidthM { get; set; }
    public List<PointDto> Points { get; set; } = new();
}

public class LayoutDto
{
    public Guid? Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid SimulationRunId { get; set; }
    public int VersionNo { get; set; }

    // Auto or Manual
    public string Origin { get; set; } = "Auto";

    public Guid? RuleSetId { get; set; }
    public int? RuleSetVersion { get; set; }

    // Always true: the layout is a draft and never a compliance claim
    public bool IsDraft { get; set; } = true;

    public PointDto GatePoint { get; set; } = new();
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<LayoutItemDto> Items { get; set; } = new();
    public List<LayoutWarningDto> Warnings { get; set; } = new();
    public List<LayoutRoadDto> Roads { get; set; } = new();

    public string? SvgMarkup { get; set; }
}
