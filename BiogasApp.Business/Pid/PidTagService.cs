using BiogasApp.Database.Enums;

namespace BiogasApp.Business.Pid;

public class PidTagService
{
    public const int FirstSeries = 101;

    public static string PrefixOf(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return string.Empty;
        var s = tag.Trim();
        var i = s.IndexOf('-');
        return i < 0 ? s : s[..i];
    }

    public static string EquipmentPrefix(PidSymbolType type) => type switch
    {
        PidSymbolType.Tank => "T",
        PidSymbolType.Pump => "P",
        PidSymbolType.Valve => "V",
        PidSymbolType.Blower => "BL",
        PidSymbolType.Flare => "FL",
        PidSymbolType.Digester => "D",
        PidSymbolType.GasHolder => "GH",
        PidSymbolType.Vessel => "VS",
        PidSymbolType.Instrument => "I",
        _ => "X"
    };

    public static string MediumCode(PidMedium medium) => medium switch
    {
        PidMedium.Slurry => "SL",
        PidMedium.Biogas => "BG",
        PidMedium.Cbg => "CBG",
        PidMedium.Water => "W",
        PidMedium.Digestate => "DG",
        PidMedium.Steam => "ST",
        PidMedium.Air => "AIR",
        PidMedium.Condensate => "CD",
        _ => "OT"
    };

    public string Next(string prefix, IEnumerable<string?> existing)
    {
        var max = FirstSeries - 1;
        foreach (var tag in existing)
        {
            if (string.IsNullOrWhiteSpace(tag)) continue;
            var s = tag.Trim();
            var i = s.LastIndexOf('-');
            if (i <= 0) continue;
            if (!s[..i].Equals(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (int.TryParse(s[(i + 1)..], out var num) && num > max) max = num;
        }
        return $"{prefix}-{max + 1}";
    }

    public string NextEquipment(PidSymbolType type, IEnumerable<string?> existing)
        => Next(EquipmentPrefix(type), existing);

    public string NextPump(IEnumerable<string?> existing) => Next("P", existing);

    public string NextValve(IEnumerable<string?> existing) => Next("V", existing);

    public string NextInstrument(PidInstrumentType type, IEnumerable<string?> existing)
        => Next(type.ToString(), existing);

    public string NextLineNo(PidMedium medium, decimal sizeMm, IEnumerable<string?> existing)
    {
        var max = FirstSeries - 1;
        foreach (var lineNo in existing)
        {
            if (string.IsNullOrWhiteSpace(lineNo)) continue;
            var s = lineNo.Trim();
            var i = s.LastIndexOf('-');
            if (i < 0) continue;
            if (int.TryParse(s[(i + 1)..], out var num) && num > max) max = num;
        }
        return $"{sizeMm:0}-{MediumCode(medium)}-{max + 1}";
    }
}
