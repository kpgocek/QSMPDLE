using MudBlazor.Services;
using QSMPDLE.Web.Components;
using QSMPDLE.Web.Features.Communication;
using QSMPDLE.Web.Features.Gameplay;
using QSMPDLE.Web.Features.Sharing;
using QSMPDLE.Web.Features.Statistics;
using QSMPDLE.Web.Infrastructure;
using QSMPDLE.Web.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddLocalStorageServices();

builder.Services.AddMemoryCache();

builder.Services.AddMudServices();
builder.Services.AddBlazorBootstrap();

builder.AddNpgsqlDbContext<ApplicationDbContext>("qsmpdle");

builder.Services.AddInfrastructure();

builder.Services.AddInternalCommunication();

builder.Services.AddGameplay();

builder.Services.AddSharingFeature();
builder.Services.AddStatistics();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = true);

var app = builder.Build();

app.MapDefaultEndpoints();

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
