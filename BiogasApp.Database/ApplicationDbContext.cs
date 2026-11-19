using System.Text;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BiogasApp.Database;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Feedstock> Feedstocks => Set<Feedstock>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectFeedstockMix> ProjectFeedstockMixes => Set<ProjectFeedstockMix>();
    public DbSet<SimulationRun> SimulationRuns => Set<SimulationRun>();
    public DbSet<SizedEquipment> SizedEquipment => Set<SizedEquipment>();
    public DbSet<VendorRateCard> VendorRateCards => Set<VendorRateCard>();
    public DbSet<GeneratedArtifact> GeneratedArtifacts => Set<GeneratedArtifact>();
    public DbSet<CadJob> CadJobs => Set<CadJob>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<ExceptionLog> ExceptionLogs => Set<ExceptionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- Enum <-> DB string converters ----------
        var feedstockCategory = EnumToDb(new Dictionary<FeedstockCategory, string>
        {
            [FeedstockCategory.Agro] = "Agro",
            [FeedstockCategory.Manure] = "Manure",
            [FeedstockCategory.Industrial] = "Industrial",
            [FeedstockCategory.Msw] = "MSW"
        });

        var regulatoryStandard = EnumToDb(new Dictionary<RegulatoryStandard, string>
        {
            [RegulatoryStandard.PesoIndia] = "PESO_INDIA",
            [RegulatoryStandard.AtexEu] = "ATEX_EU"
        });

        var digesterRegime = EnumToDb(new Dictionary<DigesterRegime, string>
        {
            [DigesterRegime.Mesophilic] = "MESOPHILIC",
            [DigesterRegime.Thermophilic] = "THERMOPHILIC"
        });

        var upgradingTech = EnumToDb(new Dictionary<UpgradingTech, string>
        {
            [UpgradingTech.Psa] = "PSA",
            [UpgradingTech.Membrane] = "MEMBRANE",
            [UpgradingTech.Hpws] = "HPWS"
        });

        var projectStatus = EnumToDb(new Dictionary<ProjectStatus, string>
        {
            [ProjectStatus.Draft] = "DRAFT",
            [ProjectStatus.Simulated] = "SIMULATED",
            [ProjectStatus.Approved] = "APPROVED",
            [ProjectStatus.Archived] = "ARCHIVED"
        });

        var artifactType = EnumToDb(new Dictionary<ArtifactType, string>
        {
            [ArtifactType.GaDxf] = "GA_DXF",
            [ArtifactType.PfdDxf] = "PFD_DXF",
            [ArtifactType.ShellDevDxf] = "SHELL_DEV_DXF",
            [ArtifactType.Model3dStep] = "MODEL_3D_STEP",
            [ArtifactType.BomXlsx] = "BOM_XLSX",
            [ArtifactType.ProposalPdf] = "PROPOSAL_PDF"
        });

        var cadJobStatus = EnumToDb(new Dictionary<CadJobStatus, string>
        {
            [CadJobStatus.Queued] = "QUEUED",
            [CadJobStatus.Running] = "RUNNING",
            [CadJobStatus.Completed] = "COMPLETED",
            [CadJobStatus.Failed] = "FAILED"
        });

        // ---------- feedstocks ----------
        modelBuilder.Entity<Feedstock>(e =>
        {
            e.ToTable("feedstocks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique().HasDatabaseName("uq_feedstocks_name");
            e.Property(x => x.Category).HasConversion(feedstockCategory).HasMaxLength(50).IsRequired();
            e.Property(x => x.TsPercent).HasPrecision(5, 2);
            e.Property(x => x.VsPercentOfTs).HasPrecision(5, 2);
            e.Property(x => x.CnRatio).HasPrecision(5, 2);
            e.Property(x => x.BmpNm3PerKgVs).HasPrecision(6, 4);
            e.Property(x => x.BulkDensityKgM3).HasPrecision(7, 2);
            e.Property(x => x.DegradationFactor).HasPrecision(4, 3).HasDefaultValue(0.850m);
            e.Property(x => x.DefaultCostPerTon).HasPrecision(10, 2).HasDefaultValue(0.00m);
            e.Property(x => x.CreatedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // ---------- projects ----------
        modelBuilder.Entity<Project>(e =>
        {
            e.ToTable("projects");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.LocationCountry).HasMaxLength(100).IsRequired();
            e.Property(x => x.RegulatoryStandard).HasConversion(regulatoryStandard).HasMaxLength(30).IsRequired();
            e.Property(x => x.AmbientTempMinC).HasPrecision(4, 1);
            e.Property(x => x.AmbientTempMaxC).HasPrecision(4, 1);
            e.Property(x => x.DigesterRegime).HasConversion(digesterRegime).HasMaxLength(20).IsRequired();
            e.Property(x => x.UpgradingTech).HasConversion(upgradingTech).HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasConversion(projectStatus).HasMaxLength(20).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // ---------- project_feedstock_mix ----------
        modelBuilder.Entity<ProjectFeedstockMix>(e =>
        {
            e.ToTable("project_feedstock_mix");
            e.HasKey(x => x.Id);
            e.Property(x => x.DailyTonnageTpd).HasPrecision(8, 2);
            e.Property(x => x.SeasonalAvailabilityDays).HasDefaultValue(365);
            e.Property(x => x.CustomTsOverride).HasPrecision(5, 2);
            e.Property(x => x.CustomVsOverride).HasPrecision(5, 2);
            e.HasIndex(x => new { x.ProjectId, x.FeedstockId }).IsUnique().HasDatabaseName("uq_project_feedstock");

            e.HasOne(x => x.Project)
                .WithMany(p => p.FeedstockMix)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Feedstock)
                .WithMany(f => f.ProjectMixes)
                .HasForeignKey(x => x.FeedstockId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- simulation_runs ----------
        modelBuilder.Entity<SimulationRun>(e =>
        {
            e.ToTable("simulation_runs");
            e.HasKey(x => x.Id);
            e.Property(x => x.RunVersion).HasDefaultValue(1);
            e.Property(x => x.TotalFeedTpd).HasPrecision(8, 2);
            e.Property(x => x.BlendedTsPercent).HasPrecision(5, 2);
            e.Property(x => x.BlendedVsTpd).HasPrecision(8, 2);
            e.Property(x => x.BlendedCnRatio).HasPrecision(5, 2);
            e.Property(x => x.HrtDays).HasPrecision(5, 2);
            e.Property(x => x.OlrKgVsM3Day).HasPrecision(5, 2);
            e.Property(x => x.RawBiogasNm3Day).HasPrecision(10, 2);
            e.Property(x => x.Ch4Percent).HasPrecision(4, 2);
            e.Property(x => x.Co2Percent).HasPrecision(4, 2);
            e.Property(x => x.H2sPpm).HasPrecision(7, 1);
            e.Property(x => x.NetCbgKgDay).HasPrecision(10, 2);
            e.Property(x => x.ParasiticHeatKw).HasPrecision(8, 2);
            e.Property(x => x.ParasiticPowerKw).HasPrecision(8, 2);
            e.Property(x => x.WarningsJson).HasColumnType("json").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");

            e.HasOne(x => x.Project)
                .WithMany(p => p.SimulationRuns)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- sized_equipment ----------
        modelBuilder.Entity<SizedEquipment>(e =>
        {
            e.ToTable("sized_equipment");
            e.HasKey(x => x.Id);
            e.Property(x => x.EquipmentTag).HasMaxLength(30).IsRequired();
            e.Property(x => x.EquipmentType).HasMaxLength(50).IsRequired();
            e.Property(x => x.Quantity).HasDefaultValue(1);
            e.Property(x => x.GeometryParamsJson).HasColumnType("json");
            e.Property(x => x.ProcessDutyJson).HasColumnType("json").IsRequired();
            e.Property(x => x.UnitCost).HasPrecision(12, 2);
            e.Property(x => x.TotalCost).HasPrecision(14, 2);

            e.HasOne(x => x.SimulationRun)
                .WithMany(r => r.Equipment)
                .HasForeignKey(x => x.SimulationRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- vendor_rate_cards ----------
        modelBuilder.Entity<VendorRateCard>(e =>
        {
            e.ToTable("vendor_rate_cards");
            e.HasKey(x => x.Id);
            e.Property(x => x.ItemCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.Category).HasMaxLength(30).IsRequired();
            e.Property(x => x.Description).HasColumnType("text").IsRequired();
            e.Property(x => x.Unit).HasMaxLength(20).IsRequired();
            e.Property(x => x.UnitRate).HasPrecision(10, 2);
            e.Property(x => x.Currency).HasMaxLength(3).IsFixedLength().HasDefaultValue("INR");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");

            e.HasOne(x => x.Tenant)
                .WithMany(t => t.RateCards)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- generated_artifacts ----------
        modelBuilder.Entity<GeneratedArtifact>(e =>
        {
            e.ToTable("generated_artifacts");
            e.HasKey(x => x.Id);
            e.Property(x => x.ArtifactType).HasConversion(artifactType).HasMaxLength(30).IsRequired();
            e.Property(x => x.FileUrl).HasColumnType("text").IsRequired();
            e.Property(x => x.Checksum).HasMaxLength(64).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");

            e.HasOne(x => x.SimulationRun)
                .WithMany(r => r.Artifacts)
                .HasForeignKey(x => x.SimulationRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- cad_jobs ----------
        modelBuilder.Entity<CadJob>(e =>
        {
            e.ToTable("cad_jobs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion(cadJobStatus).HasMaxLength(20).IsRequired();
            e.Property(x => x.ExportFormatsJson).HasColumnType("json").IsRequired();
            e.Property(x => x.ErrorMessage).HasColumnType("text");

            e.HasOne(x => x.SimulationRun)
                .WithMany(r => r.CadJobs)
                .HasForeignKey(x => x.SimulationRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- tenants ----------
        modelBuilder.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
        });

        // ---------- user_roles ----------
        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        // ---------- app_users ----------
        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("app_users");
            e.HasKey(x => x.Id);
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();

            e.HasOne(x => x.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- exception_logs ----------
        modelBuilder.Entity<ExceptionLog>(e =>
        {
            e.ToTable("exception_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Message).HasColumnType("text").IsRequired();
            e.Property(x => x.StackTrace).HasColumnType("text");
            e.Property(x => x.Source).HasMaxLength(200);
            e.Property(x => x.RequestPath).HasMaxLength(500);
        });

        // ---------- snake_case column names for every property ----------
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static ValueConverter<TEnum, string> EnumToDb<TEnum>(Dictionary<TEnum, string> map)
        where TEnum : struct, Enum
    {
        var reverse = map.ToDictionary(x => x.Value, x => x.Key);
        return new ValueConverter<TEnum, string>(v => map[v], s => reverse[s]);
    }

    private static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])))
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}