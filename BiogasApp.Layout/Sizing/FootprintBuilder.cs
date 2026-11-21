using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Layout.Models;

namespace BiogasApp.Layout.Sizing;

public static class FootprintBuilder
{
    private const double DefaultSizeM = 5.0;

    public static List<EquipmentInput> FromItems(IEnumerable<ProjectEquipmentItem> items)
    {
        var result = new List<EquipmentInput>();

        foreach (var item in items.Where(i => i.IsEnabled))
        {
            double w = (double)(item.WidthM ?? 0m);
            double l = (double)(item.LengthM ?? 0m);
            double d = (double)(item.DiameterM ?? 0m);

            if (item.Shape == EquipmentShape.Circle)
            {
                if (d <= 0) d = Math.Max(w, l);
                if (d <= 0) d = Fallback(item.Tag)?.DiameterM ?? DefaultSizeM;
                w = d;
                l = d;
            }
            else
            {
                if (w <= 0 || l <= 0)
                {
                    var std = Fallback(item.Tag);
                    if (w <= 0) w = std?.WidthM ?? DefaultSizeM;
                    if (l <= 0) l = std?.LengthM ?? DefaultSizeM;
                }
                d = 0;
            }

            int qty = Math.Max(1, item.Quantity);

            result.Add(new EquipmentInput(
                item.Id, item.Tag, item.Name,
                item.HazardClass, item.Zone, item.Shape,
                w, l, d, qty, item.AllowRotation, item.NeedsRoadAccess));
        }

        return result;
    }

    public static List<PlacedItem> Expand(IEnumerable<EquipmentInput> equipment)
    {
        var list = new List<PlacedItem>();
        foreach (var e in equipment)
        {
            for (int n = 1; n <= e.Quantity; n++)
                list.Add(NewPlaced(e, n));
        }
        return list;
    }

    public static PlacedItem NewPlaced(EquipmentInput e, int instanceNo) => new()
    {
        EquipmentId = e.Id,
        InstanceNo = instanceNo,
        Tag = e.Tag,
        Name = e.Name,
        HazardClass = e.HazardClass,
        Zone = e.Zone,
        Shape = e.Shape,
        WidthM = e.Shape == EquipmentShape.Circle ? e.DiameterM : e.WidthM,
        LengthM = e.Shape == EquipmentShape.Circle ? e.DiameterM : e.LengthM,
        NeedsRoadAccess = e.NeedsRoadAccess
    };

    private static StandardFootprint? Fallback(string tag)
    {
        string prefix = new string((tag ?? string.Empty).TakeWhile(char.IsLetter).ToArray());
        if (prefix.Length == 0) return null;
        return StandardFootprints.All.FirstOrDefault(
            x => string.Equals(x.TagPrefix, prefix, StringComparison.OrdinalIgnoreCase));
    }
}