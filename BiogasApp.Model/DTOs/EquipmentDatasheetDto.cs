using BiogasApp.Database.Enums;

namespace BiogasApp.Model.DTOs;

public class EquipmentDatasheetSetDto
{
    public Guid PidDiagramId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string DrawingNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int RevisionNo { get; set; }
    public PidApprovalStatus Status { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<EquipmentDatasheetDto> Items { get; set; } = new();
}

public class EquipmentDatasheetDto
{
    public Guid NodeId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EquipmentType { get; set; } = string.Empty;
    public PidSymbolType SymbolType { get; set; }
    public int Quantity { get; set; } = 1;

    public string? Zone { get; set; }
    public string? HazardClass { get; set; }
    public string? Shape { get; set; }
    public decimal? WidthM { get; set; }
    public decimal? LengthM { get; set; }
    public decimal? DiameterM { get; set; }
    public bool? NeedsRoadAccess { get; set; }

    public List<DatasheetFieldDto> Process { get; set; } = new();
    public List<DatasheetFieldDto> Mechanical { get; set; } = new();
    public List<DatasheetFieldDto> Utilities { get; set; } = new();
    public List<DatasheetFieldDto> Notes { get; set; } = new();

    public List<PidInstrumentDto> Instruments { get; set; } = new();
    public List<PidValveDto> Valves { get; set; } = new();
    public List<PidLineDto> ConnectedLines { get; set; } = new();

    public EquipmentVendorDataDto Vendor { get; set; } = new();
}

public class DatasheetFieldDto
{
    public string Label { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Unit { get; set; }
}

public class EquipmentVendorDataDto
{
    public string Tag { get; set; } = string.Empty;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Material { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public int? DeliveryWeeks { get; set; }
    public string? Remarks { get; set; }
}
