namespace BiogasApp.Database.Enums;

public enum PidMedium
{
    Slurry = 1,
    Biogas = 2,
    Cbg = 3,
    Water = 4,
    Digestate = 5,
    Steam = 6,
    Air = 7,
    Condensate = 8,
    Other = 9
}

public enum PidValveType
{
    Gate = 1,
    Ball = 2,
    Butterfly = 3,
    Check = 4,
    Globe = 5,
    Relief = 6,
    Control = 7,
    Solenoid = 8
}

public enum PidInstrumentType
{
    PT = 1,
    TT = 2,
    FT = 3,
    LT = 4
}

public enum PidSymbolType
{
    Tank = 1,
    Pump = 2,
    Valve = 3,
    Blower = 4,
    Flare = 5,
    Digester = 6,
    GasHolder = 7,
    Vessel = 8,
    Instrument = 9
}

public enum PidApprovalStatus
{
    Draft = 1,
    InReview = 2,
    Approved = 3,
    Rejected = 4,
    Superseded = 5
}
