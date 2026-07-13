using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
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


builder.Services.AddDbContextFactory<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

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

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var extension = Path.GetExtension(context.File.Name);
        if (IsBrowserCacheableAsset(extension))
        {
            context.Context.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromDays(30)
            };
        }
    }
});

app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.MigrateDatabasesAsync();

app.Run();

static bool IsBrowserCacheableAsset(string extension) =>
    extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
    || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
    || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
    || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
    || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
    || extension.Equals(".woff2", StringComparison.OrdinalIgnoreCase)
    || extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
    || extension.Equals(".css", StringComparison.OrdinalIgnoreCase)
    || extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
    || extension.Equals(".br", StringComparison.OrdinalIgnoreCase)
    || extension.Equals(".gz", StringComparison.OrdinalIgnoreCase);
