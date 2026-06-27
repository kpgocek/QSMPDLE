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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalStorageServices();

builder.Services.AddMemoryCache();

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
