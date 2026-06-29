using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Npgsql;
using QSMPDLE.Web.Components;
using QSMPDLE.Web.Features.Communication;
using QSMPDLE.Web.Features.Gameplay;
using QSMPDLE.Web.Features.Sharing;
using QSMPDLE.Web.Features.Statistics;
using QSMPDLE.Web.Infrastructure;
using QSMPDLE.Web.Infrastructure.Persistence;
using QSMPDLE.Web.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalStorageServices();

builder.Services.AddMemoryCache();

builder.Services.AddHostedService<StatisticsRefreshWorker>();

builder.Services.AddMudServices();
builder.Services.AddBlazorBootstrap();

var connectionString = new NpgsqlConnectionStringBuilder
{
    Host = builder.Configuration["PGHOST"],
    Port = int.Parse(builder.Configuration["PGPORT"]!),
    Database = builder.Configuration["PGDATABASE"],
    Username = builder.Configuration["PGUSER"],
    Password = builder.Configuration["PGPASSWORD"]
}.ConnectionString;


System.Diagnostics.Debug.WriteLine($"PGHOST = {builder.Configuration["PGHOST"]}");
System.Diagnostics.Debug.WriteLine($"PGPORT = {builder.Configuration["PGPORT"]}");
System.Diagnostics.Debug.WriteLine($"PGDATABASE = {builder.Configuration["PGDATABASE"]}");
System.Diagnostics.Debug.WriteLine($"PGUSER = {builder.Configuration["PGUSER"]}");
System.Diagnostics.Debug.WriteLine($"PGPASSWORD = {builder.Configuration["PGPASSWORD"]}");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddInfrastructure();

builder.Services.AddInternalCommunication();

builder.Services.AddGameplay();

builder.Services.AddSharingFeature();
builder.Services.AddStatistics();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = true);

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
