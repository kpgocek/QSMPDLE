using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using QSMPDLE.Web.Components;
using QSMPDLE.Web.Features.Communication;
using QSMPDLE.Web.Features.Gameplay;
using QSMPDLE.Web.Features.Sharing;
using QSMPDLE.Web.Features.Statistics;
using QSMPDLE.Web.Infrastructure;
using QSMPDLE.Web.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalStorageServices();

builder.Services.AddMemoryCache();

builder.Services.AddMudServices();

var dataDbPath = Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["Database:DataPath"]!);
var statsDbPath = Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["Database:StatsPath"]!);

builder.Services.AddDbContext<GameplayDbContext>(options => options.UseSqlite($"Data Source={dataDbPath}"));
builder.Services.AddDbContext<TelemetryDbContext>(options => options.UseSqlite($"Data Source={statsDbPath}"));

builder.Services.AddInfrastructure();

builder.Services.AddInternalCommunication();

builder.Services.AddGameplay();

builder.Services.AddSharingFeature();
builder.Services.AddStatistics();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.MigrateDatabasesAsync();

app.Run();
