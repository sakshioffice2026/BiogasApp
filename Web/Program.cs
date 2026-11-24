using BiogasApp.Business.Engine;
using BiogasApp.Business.Financial;
using BiogasApp.Business.Pid;
using BiogasApp.Database;
using BiogasApp.Repositories.Contracts;
using BiogasApp.Repositories.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- MVC ----------
builder.Services.AddControllersWithViews();

// ---------- Database (MySQL) ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing in appsettings.json.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));

// ---------- Authentication (cookie) ----------
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
    });

// ---------- Repositories ----------
builder.Services.AddScoped<IUnitOfWork, UnitOfWorks>();
builder.Services.AddScoped<IAccount, AccountRepository>();
builder.Services.AddScoped<ITenant, TenantRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IFeedstockRepository, FeedstockRepository>();
builder.Services.AddScoped<ISimulationRepository, SimulationRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IRateCardRepository, RateCardRepository>();
builder.Services.AddScoped<IRazorViewRenderer, RazorViewRenderRepository>();
builder.Services.AddScoped<IProposalExport, ProposalExportRepository>();
builder.Services.AddScoped<ExceptionHandlerRepository>();
builder.Services.AddScoped<IEnquiryRepository, EnquiryRepository>();
builder.Services.AddScoped<IEnquiryProposalRepository, EnquiryProposalRepository>();
builder.Services.AddScoped<IEnquiryInviteRepository, EnquiryInviteRepository>();
builder.Services.AddScoped<ISiteRepository, SiteRepository>();
builder.Services.AddScoped<IClearanceRuleRepository, ClearanceRuleRepository>();
builder.Services.AddScoped<ISiteLayoutRepository, SiteLayoutRepository>();
builder.Services.AddScoped<IAssessmentRepository, AssessmentRepository>();
builder.Services.AddScoped<IPidRepository, PidRepository>();
builder.Services.AddScoped<IPidExport, PidExportRepository>();
builder.Services.AddScoped<PidTagService>();
builder.Services.AddScoped<PidTemplateBuilder>();
builder.Services.AddScoped<PidAutoLayoutService>();
// ---------- CAD generation (built-in layout engine) ----------
builder.Services.AddScoped<ICadGeneration, CadGenerationRepository>();
builder.Services.AddHttpClient<IReceptorProvider, OsmReceptorProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(40);
});
// ---------- Artifact storage (local disk) ----------
var artifactRoot = builder.Configuration["ArtifactStorage:RootPath"];
if (string.IsNullOrWhiteSpace(artifactRoot))
{
    artifactRoot = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "artifacts");
}
builder.Services.AddSingleton<IArtifactStorage>(_ => new LocalArtifactStorage(artifactRoot));

// ---------- Calculation engine ----------
builder.Services.AddScoped<FeedstockBlendingService>();
builder.Services.AddScoped<DigesterSizingService>();
builder.Services.AddScoped<GasYieldService>();
builder.Services.AddScoped<HeatBalanceService>();
builder.Services.AddScoped<MechanicalSizingService>();
builder.Services.AddScoped<UpgradingService>();
builder.Services.AddScoped<DigestateBomService>();
builder.Services.AddScoped<PreTreatmentService>();
builder.Services.AddScoped<CbgDispatchService>();
builder.Services.AddScoped<ICalculationEngine, CalculationEngine>();

var app = builder.Build();

// ---------- Seed reference data ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await SeedData.InitializeAsync(db);
}

// ---------- HTTP pipeline ----------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();