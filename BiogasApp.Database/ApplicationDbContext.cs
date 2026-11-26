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
    public DbSet<Enquiry> Enquiries => Set<Enquiry>();
    public DbSet<EnquiryInvite> EnquiryInvites => Set<EnquiryInvite>();
    public DbSet<EnquiryProposal> EnquiryProposals => Set<EnquiryProposal>();
    public DbSet<ClearanceRuleSet> ClearanceRuleSets => Set<ClearanceRuleSet>();
    public DbSet<ClearanceRule> ClearanceRules => Set<ClearanceRule>();
    public DbSet<ClearanceSetting> ClearanceSettings => Set<ClearanceSetting>();
    public DbSet<ProjectSite> ProjectSites => Set<ProjectSite>();
    public DbSet<SiteReceptor> SiteReceptors => Set<SiteReceptor>();
    public DbSet<NoBuildZone> NoBuildZones => Set<NoBuildZone>();
    public DbSet<ProjectEquipmentItem> ProjectEquipmentItems => Set<ProjectEquipmentItem>();
    public DbSet<SiteLayout> SiteLayouts => Set<SiteLayout>();
    public DbSet<SiteLayoutItem> SiteLayoutItems => Set<SiteLayoutItem>();
    public DbSet<SiteAssessment> SiteAssessments => Set<SiteAssessment>();
    public DbSet<AssessmentReceptor> AssessmentReceptors => Set<AssessmentReceptor>();
    public DbSet<AssessmentNoBuildZone> AssessmentNoBuildZones => Set<AssessmentNoBuildZone>();
    public DbSet<AssessmentEquipmentItem> AssessmentEquipmentItems => Set<AssessmentEquipmentItem>();
    public DbSet<AssessmentLayout> AssessmentLayouts => Set<AssessmentLayout>();
    public DbSet<AssessmentLayoutItem> AssessmentLayoutItems => Set<AssessmentLayoutItem>();

    public DbSet<PidDiagram> PidDiagrams => Set<PidDiagram>();
    public DbSet<PidEquipmentNode> PidEquipmentNodes => Set<PidEquipmentNode>();
    public DbSet<PidLine> PidLines => Set<PidLine>();
    public DbSet<PidValve> PidValves => Set<PidValve>();
    public DbSet<PidInstrument> PidInstruments => Set<PidInstrument>();
    public DbSet<PidRevision> PidRevisions => Set<PidRevision>();
    public DbSet<PidEquipmentVendorData> PidEquipmentVendorData => Set<PidEquipmentVendorData>();
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

        var enquiryStatus = EnumToDb(new Dictionary<EnquiryStatus, string>
        {
            [EnquiryStatus.New] = "NEW",
            [EnquiryStatus.UnderReview] = "UNDER_REVIEW",
            [EnquiryStatus.ProposalSent] = "PROPOSAL_SENT",
            [EnquiryStatus.Revised] = "REVISED",
            [EnquiryStatus.Approved] = "APPROVED",
            [EnquiryStatus.Rejected] = "REJECTED"
        });

        var enquirySource = EnumToDb(new Dictionary<EnquirySource, string>
        {
            [EnquirySource.Website] = "WEBSITE",
            [EnquirySource.Email] = "EMAIL",
            [EnquirySource.Phone] = "PHONE",
            [EnquirySource.WhatsApp] = "WHATSAPP",
            [EnquirySource.Referral] = "REFERRAL",
            [EnquirySource.Exhibition] = "EXHIBITION",
            [EnquirySource.Other] = "OTHER"
        });

        var proposalStatus = EnumToDb(new Dictionary<ProposalStatus, string>
        {
            [ProposalStatus.Draft] = "DRAFT",
            [ProposalStatus.Sent] = "SENT",
            [ProposalStatus.Approved] = "APPROVED",
            [ProposalStatus.Rejected] = "REJECTED",
            [ProposalStatus.Superseded] = "SUPERSEDED"
        });

        var inviteStatus = EnumToDb(new Dictionary<InviteStatus, string>
        {
            [InviteStatus.Pending] = "PENDING",
            [InviteStatus.Submitted] = "SUBMITTED",
            [InviteStatus.Expired] = "EXPIRED",
            [InviteStatus.Cancelled] = "CANCELLED"
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

        var hazardClass = EnumToDb(new Dictionary<HazardClass, string>
        {
            [HazardClass.Cascade] = "CASCADE",
            [HazardClass.Compressor] = "COMPRESSOR",
            [HazardClass.Dispenser] = "DISPENSER",
            [HazardClass.GasVessel] = "GAS_VESSEL",
            [HazardClass.DigesterGas] = "DIGESTER_GAS",
            [HazardClass.Scrubber] = "SCRUBBER",
            [HazardClass.Flare] = "FLARE",
            [HazardClass.Electrical] = "ELECTRICAL",
            [HazardClass.Building] = "BUILDING",
            [HazardClass.Road] = "ROAD",
            [HazardClass.Boundary] = "BOUNDARY",
            [HazardClass.Receptor] = "RECEPTOR",
            [HazardClass.Tank] = "TANK",
            [HazardClass.OpenYard] = "OPEN_YARD",
            [HazardClass.Other] = "OTHER"
        });

        var ruleStatus = EnumToDb(new Dictionary<RuleStatus, string>
        {
            [RuleStatus.Official] = "Official",
            [RuleStatus.Reference] = "Reference",
            [RuleStatus.Assumed] = "Assumed"
        });

        var receptorType = EnumToDb(new Dictionary<ReceptorType, string>
        {
            [ReceptorType.School] = "SCHOOL",
            [ReceptorType.Hospital] = "HOSPITAL",
            [ReceptorType.House] = "HOUSE",
            [ReceptorType.Road] = "ROAD",
            [ReceptorType.Canal] = "CANAL",
            [ReceptorType.PowerLine] = "POWER_LINE",
            [ReceptorType.Other] = "OTHER"
        });

        var receptorSource = EnumToDb(new Dictionary<ReceptorSource, string>
        {
            [ReceptorSource.MapData] = "MAP_DATA",
            [ReceptorSource.UserAdded] = "USER_ADDED"
        });

        var noBuildReason = EnumToDb(new Dictionary<NoBuildReason, string>
        {
            [NoBuildReason.HighTensionLine] = "HIGH_TENSION_LINE",
            [NoBuildReason.Canal] = "CANAL",
            [NoBuildReason.Pipeline] = "PIPELINE",
            [NoBuildReason.Easement] = "EASEMENT",
            [NoBuildReason.RoadSetback] = "ROAD_SETBACK",
            [NoBuildReason.Other] = "OTHER"
        });

        var equipmentZone = EnumToDb(new Dictionary<EquipmentZone, string>
        {
            [EquipmentZone.Dirty] = "DIRTY",
            [EquipmentZone.Process] = "PROCESS",
            [EquipmentZone.Gas] = "GAS",
            [EquipmentZone.Clean] = "CLEAN"
        });

        var equipmentShape = EnumToDb(new Dictionary<EquipmentShape, string>
        {
            [EquipmentShape.Circle] = "CIRCLE",
            [EquipmentShape.Rectangle] = "RECTANGLE"
        });

        var sizeSource = EnumToDb(new Dictionary<SizeSource, string>
        {
            [SizeSource.Simulation] = "SIMULATION",
            [SizeSource.Standard] = "STANDARD",
            [SizeSource.UserOverride] = "USER_OVERRIDE"
        });

        var layoutOrigin = EnumToDb(new Dictionary<LayoutOrigin, string>
        {
            [LayoutOrigin.Auto] = "AUTO",
            [LayoutOrigin.Manual] = "MANUAL"
        });

        var assessmentStatus = EnumToDb(new Dictionary<AssessmentStatus, string>
        {
            [AssessmentStatus.Draft] = "DRAFT",
            [AssessmentStatus.Converted] = "CONVERTED"
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

            e.HasOne(x => x.SiteLayout)
                .WithMany()
                .HasForeignKey(x => x.SiteLayoutId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------- enquiries ----------
        modelBuilder.Entity<Enquiry>(e =>
        {
            e.ToTable("enquiries");
            e.HasKey(x => x.Id);
            e.Property(x => x.EnquiryNo).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.EnquiryNo).IsUnique().HasDatabaseName("uq_enquiries_enquiry_no");
            e.Property(x => x.CustomerName).HasMaxLength(150).IsRequired();
            e.Property(x => x.CompanyName).HasMaxLength(150);
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.LocationCity).HasMaxLength(100).IsRequired();
            e.Property(x => x.LocationState).HasMaxLength(100).IsRequired();
            e.Property(x => x.LocationCountry).HasMaxLength(100).IsRequired();
            e.Property(x => x.FeedstockType).HasMaxLength(200).IsRequired();
            e.Property(x => x.FeedstockQtyTpd).HasPrecision(8, 2);
            e.Property(x => x.EndUse).HasMaxLength(50).IsRequired();
            e.Property(x => x.Message).HasColumnType("text");
            e.Property(x => x.Source).HasConversion(enquirySource).HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasConversion(enquiryStatus).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Status).HasDatabaseName("ix_enquiries_status");
            e.Property(x => x.AdminNotes).HasColumnType("text");
            e.Property(x => x.CreatedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamp");
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_enquiries_created_at");
        });

        // ---------- enquiry_proposals ----------
        modelBuilder.Entity<EnquiryProposal>(e =>
        {
            e.ToTable("enquiry_proposals");
            e.HasKey(x => x.Id);
            e.Property(x => x.ProposalNo).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.ProposalNo).IsUnique().HasDatabaseName("uq_enquiry_proposals_no");
            e.HasIndex(x => new { x.EnquiryId, x.Version }).IsUnique().HasDatabaseName("uq_enquiry_proposals_enquiry_version");
            e.Property(x => x.Status).HasConversion(proposalStatus).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Status).HasDatabaseName("ix_enquiry_proposals_status");
            e.Property(x => x.CapexTotal).HasPrecision(18, 2);
            e.Property(x => x.OpexAnnual).HasPrecision(18, 2);
            e.Property(x => x.AnnualRevenue).HasPrecision(18, 2);
            e.Property(x => x.Npv).HasPrecision(18, 2);
            e.Property(x => x.IrrPercent).HasPrecision(8, 2);
            e.Property(x => x.PaybackPeriodYears).HasPrecision(8, 2);
            e.Property(x => x.ProposalPdfUrl).HasColumnType("text");
            e.Property(x => x.ExcelBomUrl).HasColumnType("text");
            e.Property(x => x.Remarks).HasColumnType("text");
            e.Property(x => x.CreatedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.SentAt).HasColumnType("timestamp");
            e.Property(x => x.DecidedAt).HasColumnType("timestamp");

            e.HasOne(x => x.Enquiry)
                .WithMany()
                .HasForeignKey(x => x.EnquiryId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.SimulationRun)
                .WithMany()
                .HasForeignKey(x => x.SimulationRunId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- enquiry_invites ----------
        modelBuilder.Entity<EnquiryInvite>(e =>
        {
            e.ToTable("enquiry_invites");
            e.HasKey(x => x.Id);
            e.Property(x => x.Token).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Token).IsUnique().HasDatabaseName("uq_enquiry_invites_token");
            e.Property(x => x.CustomerName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion(inviteStatus).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Status).HasDatabaseName("ix_enquiry_invites_status");
            e.Property(x => x.CreatedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_enquiry_invites_created_at");
            e.Property(x => x.ExpiresAt).HasColumnType("timestamp");
            e.Property(x => x.SubmittedAt).HasColumnType("timestamp");

            e.HasOne(x => x.Enquiry)
                .WithMany()
                .HasForeignKey(x => x.EnquiryId)
                .OnDelete(DeleteBehavior.SetNull);
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

        // ---------- clearance_rule_sets ----------
        modelBuilder.Entity<ClearanceRuleSet>(e =>
        {
            e.ToTable("clearance_rule_sets");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.RegulatoryStandard).HasConversion(regulatoryStandard).HasMaxLength(30).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new { x.Name, x.Version }).IsUnique();
        });

        // ---------- clearance_rules ----------
        modelBuilder.Entity<ClearanceRule>(e =>
        {
            e.ToTable("clearance_rules");
            e.HasKey(x => x.Id);
            e.Property(x => x.FromHazardClass).HasConversion(hazardClass).HasMaxLength(30).IsRequired();
            e.Property(x => x.ToHazardClass).HasConversion(hazardClass).HasMaxLength(30).IsRequired();
            e.Property(x => x.MinDistanceM).HasColumnType("decimal(8,2)");
            e.Property(x => x.CapacityMinL).HasColumnType("decimal(12,2)");
            e.Property(x => x.CapacityMaxL).HasColumnType("decimal(12,2)");
            e.Property(x => x.Status).HasConversion(ruleStatus).HasMaxLength(20).IsRequired();
            e.Property(x => x.SourceNote).HasMaxLength(500).IsRequired();
            e.Property(x => x.SourceUrl).HasMaxLength(500);
            e.Property(x => x.ConfirmedBy).HasMaxLength(150);
            e.Property(x => x.ConfirmedAt).HasColumnType("timestamp");
            e.HasIndex(x => new { x.RuleSetId, x.FromHazardClass, x.ToHazardClass });

            e.HasOne(x => x.RuleSet)
                .WithMany(s => s.Rules)
                .HasForeignKey(x => x.RuleSetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- clearance_settings ----------
        modelBuilder.Entity<ClearanceSetting>(e =>
        {
            e.ToTable("clearance_settings");
            e.HasKey(x => x.Id);
            e.Property(x => x.SettingKey).HasMaxLength(60).IsRequired();
            e.Property(x => x.ValueNum).HasColumnType("decimal(12,2)");
            e.Property(x => x.Unit).HasMaxLength(10).IsRequired();
            e.Property(x => x.Status).HasConversion(ruleStatus).HasMaxLength(20).IsRequired();
            e.Property(x => x.SourceNote).HasMaxLength(500).IsRequired();
            e.Property(x => x.ConfirmedBy).HasMaxLength(150);
            e.Property(x => x.ConfirmedAt).HasColumnType("timestamp");
            e.HasIndex(x => new { x.RuleSetId, x.SettingKey }).IsUnique();

            e.HasOne(x => x.RuleSet)
                .WithMany(s => s.Settings)
                .HasForeignKey(x => x.RuleSetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- project_sites ----------
        modelBuilder.Entity<ProjectSite>(e =>
        {
            e.ToTable("project_sites");
            e.HasKey(x => x.Id);
            e.Property(x => x.Latitude).HasColumnType("decimal(9,6)");
            e.Property(x => x.Longitude).HasColumnType("decimal(9,6)");
            e.Property(x => x.PlotCornersJson).HasColumnType("json").IsRequired();
            e.Property(x => x.PlotAreaM2).HasColumnType("decimal(12,2)");
            e.Property(x => x.GateOffsetM).HasColumnType("decimal(8,2)");
            e.Property(x => x.NorthAngleDeg).HasColumnType("decimal(6,2)");
            e.Property(x => x.WindFromDeg).HasColumnType("decimal(6,2)");
            e.Property(x => x.SlopeDirectionDeg).HasColumnType("decimal(6,2)");
            e.Property(x => x.PowerSideDeg).HasColumnType("decimal(6,2)");
            e.Property(x => x.GasOfftakeSideDeg).HasColumnType("decimal(6,2)");
            e.Property(x => x.Notes).HasColumnType("text");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamp");
            e.HasIndex(x => x.ProjectId).IsUnique();

            e.HasOne(x => x.Project)
                .WithOne()
                .HasForeignKey<ProjectSite>(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- site_receptors ----------
        modelBuilder.Entity<SiteReceptor>(e =>
        {
            e.ToTable("site_receptors");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion(receptorType).HasMaxLength(30).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Latitude).HasColumnType("decimal(9,6)");
            e.Property(x => x.Longitude).HasColumnType("decimal(9,6)");
            e.Property(x => x.DistanceFromPlotM).HasColumnType("decimal(10,2)");
            e.Property(x => x.Source).HasConversion(receptorSource).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.ProjectId);

            e.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- no_build_zones ----------
        modelBuilder.Entity<NoBuildZone>(e =>
        {
            e.ToTable("no_build_zones");
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasConversion(noBuildReason).HasMaxLength(30).IsRequired();
            e.Property(x => x.PolygonJson).HasColumnType("json").IsRequired();
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => x.ProjectId);

            e.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- project_equipment_items ----------
        modelBuilder.Entity<ProjectEquipmentItem>(e =>
        {
            e.ToTable("project_equipment_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tag).HasMaxLength(30).IsRequired();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.HazardClass).HasConversion(hazardClass).HasMaxLength(30).IsRequired();
            e.Property(x => x.Zone).HasConversion(equipmentZone).HasMaxLength(20).IsRequired();
            e.Property(x => x.Shape).HasConversion(equipmentShape).HasMaxLength(20).IsRequired();
            e.Property(x => x.WidthM).HasColumnType("decimal(8,2)");
            e.Property(x => x.LengthM).HasColumnType("decimal(8,2)");
            e.Property(x => x.DiameterM).HasColumnType("decimal(8,2)");
            e.Property(x => x.HeightM).HasColumnType("decimal(8,2)");
            e.Property(x => x.SizeSource).HasConversion(sizeSource).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.ProjectId, x.Tag }).IsUnique();

            e.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- site_layouts ----------
        modelBuilder.Entity<SiteLayout>(e =>
        {
            e.ToTable("site_layouts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Origin).HasConversion(layoutOrigin).HasMaxLength(10).IsRequired();
            e.Property(x => x.WarningsJson).HasColumnType("json").IsRequired();
            e.Property(x => x.CreatedBy).HasMaxLength(150);
            e.Property(x => x.CreatedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new { x.SimulationRunId, x.VersionNo }).IsUnique();

            e.HasOne(x => x.SimulationRun)
                .WithMany()
                .HasForeignKey(x => x.SimulationRunId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.RuleSet)
                .WithMany()
                .HasForeignKey(x => x.RuleSetId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------- site_layout_items ----------
        modelBuilder.Entity<SiteLayoutItem>(e =>
        {
            e.ToTable("site_layout_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.X).HasColumnType("decimal(10,2)");
            e.Property(x => x.Y).HasColumnType("decimal(10,2)");
            e.Property(x => x.RotationDeg).HasColumnType("decimal(6,2)");
            e.HasIndex(x => x.SiteLayoutId);

            e.HasOne(x => x.SiteLayout)
                .WithMany(l => l.Items)
                .HasForeignKey(x => x.SiteLayoutId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.EquipmentItem)
                .WithMany()
                .HasForeignKey(x => x.EquipmentItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        // ---------- site_assessments ----------
        modelBuilder.Entity<SiteAssessment>(e =>
        {
            e.ToTable("site_assessments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Status).HasConversion(assessmentStatus).HasMaxLength(20).IsRequired();
            e.Property(x => x.CapacityTpd).HasColumnType("decimal(8,2)");
            e.Property(x => x.Latitude).HasColumnType("decimal(9,6)");
            e.Property(x => x.Longitude).HasColumnType("decimal(9,6)");
            e.Property(x => x.PlotCornersJson).HasColumnType("json").IsRequired();
            e.Property(x => x.PlotAreaM2).HasColumnType("decimal(12,2)");
            e.Property(x => x.GateOffsetM).HasColumnType("decimal(8,2)");
            e.Property(x => x.NorthAngleDeg).HasColumnType("decimal(6,2)");
            e.Property(x => x.WindFromDeg).HasColumnType("decimal(6,2)");
            e.Property(x => x.SlopeDirectionDeg).HasColumnType("decimal(6,2)");
            e.Property(x => x.PowerSideDeg).HasColumnType("decimal(6,2)");
            e.Property(x => x.GasOfftakeSideDeg).HasColumnType("decimal(6,2)");
            e.Property(x => x.Notes).HasColumnType("text");
            e.Property(x => x.CreatedBy).HasMaxLength(150);
            e.Property(x => x.CreatedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamp");
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.EnquiryId);

            e.HasOne(x => x.Enquiry)
                .WithMany()
                .HasForeignKey(x => x.EnquiryId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------- assessment_receptors ----------
        modelBuilder.Entity<AssessmentReceptor>(e =>
        {
            e.ToTable("assessment_receptors");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion(receptorType).HasMaxLength(30).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Latitude).HasColumnType("decimal(9,6)");
            e.Property(x => x.Longitude).HasColumnType("decimal(9,6)");
            e.Property(x => x.DistanceFromPlotM).HasColumnType("decimal(10,2)");
            e.Property(x => x.Source).HasConversion(receptorSource).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.AssessmentId);

            e.HasOne(x => x.Assessment)
                .WithMany(a => a.Receptors)
                .HasForeignKey(x => x.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- assessment_no_build_zones ----------
        modelBuilder.Entity<AssessmentNoBuildZone>(e =>
        {
            e.ToTable("assessment_no_build_zones");
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasConversion(noBuildReason).HasMaxLength(30).IsRequired();
            e.Property(x => x.PolygonJson).HasColumnType("json").IsRequired();
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => x.AssessmentId);

            e.HasOne(x => x.Assessment)
                .WithMany(a => a.NoBuildZones)
                .HasForeignKey(x => x.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- assessment_equipment_items ----------
        modelBuilder.Entity<AssessmentEquipmentItem>(e =>
        {
            e.ToTable("assessment_equipment_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tag).HasMaxLength(30).IsRequired();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.HazardClass).HasConversion(hazardClass).HasMaxLength(30).IsRequired();
            e.Property(x => x.Zone).HasConversion(equipmentZone).HasMaxLength(20).IsRequired();
            e.Property(x => x.Shape).HasConversion(equipmentShape).HasMaxLength(20).IsRequired();
            e.Property(x => x.WidthM).HasColumnType("decimal(8,2)");
            e.Property(x => x.LengthM).HasColumnType("decimal(8,2)");
            e.Property(x => x.DiameterM).HasColumnType("decimal(8,2)");
            e.Property(x => x.SizeSource).HasConversion(sizeSource).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.AssessmentId, x.Tag }).IsUnique();

            e.HasOne(x => x.Assessment)
                .WithMany(a => a.Equipment)
                .HasForeignKey(x => x.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- assessment_layouts ----------
        modelBuilder.Entity<AssessmentLayout>(e =>
        {
            e.ToTable("assessment_layouts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Origin).HasConversion(layoutOrigin).HasMaxLength(10).IsRequired();
            e.Property(x => x.WarningsJson).HasColumnType("json").IsRequired();
            e.Property(x => x.CreatedBy).HasMaxLength(150);
            e.Property(x => x.CreatedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new { x.AssessmentId, x.VersionNo }).IsUnique();

            e.HasOne(x => x.Assessment)
                .WithMany(a => a.Layouts)
                .HasForeignKey(x => x.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.RuleSet)
                .WithMany()
                .HasForeignKey(x => x.RuleSetId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------- assessment_layout_items ----------
        modelBuilder.Entity<AssessmentLayoutItem>(e =>
        {
            e.ToTable("assessment_layout_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.X).HasColumnType("decimal(10,2)");
            e.Property(x => x.Y).HasColumnType("decimal(10,2)");
            e.Property(x => x.RotationDeg).HasColumnType("decimal(6,2)");
            e.HasIndex(x => x.AssessmentLayoutId);

            e.HasOne(x => x.AssessmentLayout)
                .WithMany(l => l.Items)
                .HasForeignKey(x => x.AssessmentLayoutId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.EquipmentItem)
                .WithMany()
                .HasForeignKey(x => x.EquipmentItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- pid_diagrams ----------
        modelBuilder.Entity<PidDiagram>(e =>
        {
            e.ToTable("pid_diagrams");
            e.HasKey(x => x.Id);
            e.Property(x => x.DrawingNo).HasMaxLength(50).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.ApprovedBy).HasMaxLength(150);
            e.Property(x => x.ApprovedAt).HasColumnType("timestamp");
            e.Property(x => x.CreatedBy).HasMaxLength(150);
            e.Property(x => x.CreatedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamp");
            e.HasIndex(x => new { x.ProjectId, x.SimulationRunId });

            e.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.SimulationRun)
                .WithMany()
                .HasForeignKey(x => x.SimulationRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- pid_equipment_nodes ----------
        modelBuilder.Entity<PidEquipmentNode>(e =>
        {
            e.ToTable("pid_equipment_nodes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tag).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.SymbolType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.X).HasColumnType("decimal(10,2)");
            e.Property(x => x.Y).HasColumnType("decimal(10,2)");
            e.Property(x => x.RotationDeg).HasColumnType("decimal(6,2)");
            e.HasIndex(x => x.PidDiagramId);

            e.HasOne(x => x.PidDiagram)
                .WithMany(d => d.Nodes)
                .HasForeignKey(x => x.PidDiagramId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.EquipmentItem)
                .WithMany()
                .HasForeignKey(x => x.EquipmentItemId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.SizedEquipment)
                .WithMany()
                .HasForeignKey(x => x.SizedEquipmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------- pid_lines ----------
        modelBuilder.Entity<PidLine>(e =>
        {
            e.ToTable("pid_lines");
            e.HasKey(x => x.Id);
            e.Property(x => x.LineNo).HasMaxLength(50).IsRequired();
            e.Property(x => x.Medium).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.NominalSizeMm).HasColumnType("decimal(7,2)");
            e.Property(x => x.Material).HasMaxLength(50);
            e.Property(x => x.PointsJson).HasColumnType("json").IsRequired();
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => x.PidDiagramId);

            e.HasOne(x => x.PidDiagram)
                .WithMany(d => d.Lines)
                .HasForeignKey(x => x.PidDiagramId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.FromNode)
                .WithMany()
                .HasForeignKey(x => x.FromNodeId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.ToNode)
                .WithMany()
                .HasForeignKey(x => x.ToNodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- pid_valves ----------
        modelBuilder.Entity<PidValve>(e =>
        {
            e.ToTable("pid_valves");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tag).HasMaxLength(50).IsRequired();
            e.Property(x => x.ValveType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.NominalSizeMm).HasColumnType("decimal(7,2)");
            e.Property(x => x.PositionOnLine).HasColumnType("decimal(5,4)");
            e.Property(x => x.X).HasColumnType("decimal(10,2)");
            e.Property(x => x.Y).HasColumnType("decimal(10,2)");
            e.Property(x => x.RotationDeg).HasColumnType("decimal(6,2)");
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => x.PidDiagramId);
            e.HasIndex(x => x.PidLineId);

            e.HasOne(x => x.PidDiagram)
                .WithMany(d => d.Valves)
                .HasForeignKey(x => x.PidDiagramId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.PidLine)
                .WithMany()
                .HasForeignKey(x => x.PidLineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- pid_instruments ----------
        modelBuilder.Entity<PidInstrument>(e =>
        {
            e.ToTable("pid_instruments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tag).HasMaxLength(50).IsRequired();
            e.Property(x => x.InstrumentType).HasConversion<string>().HasMaxLength(10).IsRequired();
            e.Property(x => x.Service).HasMaxLength(150);
            e.Property(x => x.RangeMin).HasColumnType("decimal(12,3)");
            e.Property(x => x.RangeMax).HasColumnType("decimal(12,3)");
            e.Property(x => x.Unit).HasMaxLength(20);
            e.Property(x => x.X).HasColumnType("decimal(10,2)");
            e.Property(x => x.Y).HasColumnType("decimal(10,2)");
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => x.PidDiagramId);

            e.HasOne(x => x.PidDiagram)
                .WithMany(d => d.Instruments)
                .HasForeignKey(x => x.PidDiagramId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.PidLine)
                .WithMany()
                .HasForeignKey(x => x.PidLineId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.PidEquipmentNode)
                .WithMany()
                .HasForeignKey(x => x.PidEquipmentNodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- pid_revisions ----------
        modelBuilder.Entity<PidRevision>(e =>
        {
            e.ToTable("pid_revisions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Remarks).HasMaxLength(500);
            e.Property(x => x.SnapshotJson).HasColumnType("longtext");
            e.Property(x => x.ChangedBy).HasMaxLength(150);
            e.Property(x => x.ChangedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new { x.PidDiagramId, x.RevisionNo });

            e.HasOne(x => x.PidDiagram)
                .WithMany(d => d.Revisions)
                .HasForeignKey(x => x.PidDiagramId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        // ---------- pid_equipment_vendor_data ----------
        modelBuilder.Entity<PidEquipmentVendorData>(e =>
        {
            e.ToTable("pid_equipment_vendor_data");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tag).HasMaxLength(50).IsRequired();
            e.Property(x => x.Make).HasMaxLength(150);
            e.Property(x => x.Model).HasMaxLength(150);
            e.Property(x => x.Material).HasMaxLength(150);
            e.Property(x => x.WeightKg).HasColumnType("decimal(12,2)");
            e.Property(x => x.Price).HasColumnType("decimal(14,2)");
            e.Property(x => x.Currency).HasMaxLength(10);
            e.Property(x => x.Remarks).HasMaxLength(500);
            e.Property(x => x.UpdatedBy).HasMaxLength(150);
            e.Property(x => x.UpdatedAt).HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new { x.PidDiagramId, x.Tag }).IsUnique();

            e.HasOne(x => x.PidDiagram)
                .WithMany()
                .HasForeignKey(x => x.PidDiagramId)
                .OnDelete(DeleteBehavior.Cascade);
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