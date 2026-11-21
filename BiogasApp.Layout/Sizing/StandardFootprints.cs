using BiogasApp.Database.Enums;

namespace BiogasApp.Layout.Sizing;

public sealed record StandardFootprint(
    string Key,
    string Name,
    string TagPrefix,
    HazardClass HazardClass,
    EquipmentZone Zone,
    EquipmentShape Shape,
    double WidthM,
    double LengthM,
    double DiameterM,
    bool NeedsRoadAccess,
    string SourceNote);

public static class StandardFootprints
{
    private const string Src = "Valor 12-TPD layout EBSPL1027 equipment schedule (Reference)";

    private static StandardFootprint Rect(string key, string name, string tag, HazardClass h,
        EquipmentZone z, double w, double l, bool road = true)
        => new(key, name, tag, h, z, EquipmentShape.Rectangle, w, l, 0, road, Src);

    private static StandardFootprint Circ(string key, string name, string tag, HazardClass h,
        EquipmentZone z, double d, bool road = false)
        => new(key, name, tag, h, z, EquipmentShape.Circle, d, d, d, road, Src);

    public static IReadOnlyList<StandardFootprint> All { get; } = new List<StandardFootprint>
    {
        Circ("digester", "Digester tank (AD)", "AD", HazardClass.DigesterGas, EquipmentZone.Process, 31, true),
        Circ("mixing_tank", "Mixing tank", "MT", HazardClass.Tank, EquipmentZone.Dirty, 8),
        Circ("feed_tank", "Feed tank", "FT", HazardClass.Tank, EquipmentZone.Dirty, 13),
        Circ("digestate_tank", "Digestate tank", "DT", HazardClass.Tank, EquipmentZone.Process, 9),

        Rect("pump_house_5x5", "Pump house", "PH", HazardClass.Building, EquipmentZone.Process, 5, 5),
        Rect("pump_house_9x6", "Pump house", "PH", HazardClass.Building, EquipmentZone.Process, 9, 6),
        Rect("pump_house_5_5x5", "Pump house", "PH", HazardClass.Building, EquipmentZone.Process, 5.5, 5),
        Rect("process_water_tank", "Process water tank", "PWT", HazardClass.Tank, EquipmentZone.Process, 6, 6, false),
        Rect("process_water_pump_house", "Process water pump house", "PWP", HazardClass.Building, EquipmentZone.Process, 5, 5),
        Rect("sls_building", "Solid-liquid separator building", "SLS", HazardClass.Building, EquipmentZone.Process, 6, 6),

        Rect("digestate_lagoon", "Digestate lagoon", "LG", HazardClass.OpenYard, EquipmentZone.Dirty, 30, 25),
        Rect("windrow_yard", "Windrow yard", "WY", HazardClass.OpenYard, EquipmentZone.Dirty, 68, 46),
        Rect("feedstock_area", "Feedstock area", "FS", HazardClass.OpenYard, EquipmentZone.Dirty, 135, 56),
        Rect("feedstock_bunkers", "Feedstock bunkers", "FB", HazardClass.OpenYard, EquipmentZone.Dirty, 100, 75),
        Rect("sun_drying_yard", "Sun drying manure yard", "DY", HazardClass.OpenYard, EquipmentZone.Dirty, 60, 50),
        Rect("manure_bagging", "Manure bagging", "MB", HazardClass.Building, EquipmentZone.Dirty, 10, 30),

        Rect("scrubber_upgrading", "Scrubber and upgradation", "SU", HazardClass.Scrubber, EquipmentZone.Gas, 25, 50),
        Rect("compressor_filling", "Compressor and filling station", "CF", HazardClass.Compressor, EquipmentZone.Gas, 25, 31.6),
        Rect("flare", "Flare unit", "FL", HazardClass.Flare, EquipmentZone.Gas, 1.5, 1.5, false),
        Rect("hot_water_generator", "Hot water generator", "HWG", HazardClass.Other, EquipmentZone.Process, 15, 25),

        Rect("mcc_room", "MCC room", "MCC", HazardClass.Electrical, EquipmentZone.Clean, 23, 10),
        Rect("transformer_dg", "Transformer yard and DG", "TR", HazardClass.Electrical, EquipmentZone.Clean, 15, 10),
        Rect("security", "Security", "SEC", HazardClass.Building, EquipmentZone.Clean, 4, 3, false),
        Rect("fire_water_tank", "Fire water tank", "FWT", HazardClass.Tank, EquipmentZone.Clean, 8, 6, false),
        Rect("fire_water_pump_house", "Fire water pump house", "FWP", HazardClass.Building, EquipmentZone.Clean, 8, 6),
        Rect("weigh_bridge", "Weigh bridge", "WB", HazardClass.Other, EquipmentZone.Clean, 5, 10),
        Rect("admin_building", "Admin building", "ADM", HazardClass.Building, EquipmentZone.Clean, 15, 10),
        Rect("parking", "Parking area", "PK", HazardClass.Other, EquipmentZone.Clean, 15, 10, false)
    };

    public static StandardFootprint? Find(string key)
        => All.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
}