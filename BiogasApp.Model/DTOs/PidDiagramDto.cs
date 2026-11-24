using BiogasApp.Database.Enums;

namespace BiogasApp.Model.DTOs;

public class PidDiagramDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid SimulationRunId { get; set; }
    public string DrawingNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int RevisionNo { get; set; }
    public PidApprovalStatus Status { get; set; }
    public List<PidNodeDto> Nodes { get; set; } = new();
    public List<PidLineDto> Lines { get; set; } = new();
    public List<PidValveDto> Valves { get; set; } = new();
    public List<PidInstrumentDto> Instruments { get; set; } = new();
}

public class PidNodeDto
{
    public Guid Id { get; set; }
    public Guid? EquipmentItemId { get; set; }
    public Guid? SizedEquipmentId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PidSymbolType SymbolType { get; set; }
    public int StageNo { get; set; }
    public int InstanceNo { get; set; } = 1;
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal RotationDeg { get; set; }
    public bool IsLocked { get; set; }
}

public class PidLineDto
{
    public Guid Id { get; set; }
    public string LineNo { get; set; } = string.Empty;
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public PidMedium Medium { get; set; }
    public decimal NominalSizeMm { get; set; }
    public string? Material { get; set; }
    public bool IsInsulated { get; set; }
    public List<PidPointDto> Points { get; set; } = new();
    public string? Notes { get; set; }
}

public class PidPointDto
{
    public decimal X { get; set; }
    public decimal Y { get; set; }
}

public class PidValveDto
{
    public Guid Id { get; set; }
    public Guid PidLineId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public PidValveType ValveType { get; set; }
    public decimal NominalSizeMm { get; set; }
    public decimal PositionOnLine { get; set; } = 0.5m;
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal RotationDeg { get; set; }
    public bool IsNormallyOpen { get; set; } = true;
    public string? Notes { get; set; }
}

public class PidInstrumentDto
{
    public Guid Id { get; set; }
    public Guid? PidLineId { get; set; }
    public Guid? PidEquipmentNodeId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public PidInstrumentType InstrumentType { get; set; }
    public string? Service { get; set; }
    public decimal? RangeMin { get; set; }
    public decimal? RangeMax { get; set; }
    public string? Unit { get; set; }
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public string? Notes { get; set; }
}
