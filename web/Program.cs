using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using MudBlazor.Services;
using Npgsql;
using QSMPDLE.Web.Components;
using QSMPDLE.Web.Diagnostics;
using QSMPDLE.Web.Features.Archive;
using QSMPDLE.Web.Features.Communication;
using QSMPDLE.Web.Features.Gameplay;
using QSMPDLE.Web.Features.Sharing;
using QSMPDLE.Web.Features.Sitemap;
using QSMPDLE.Web.Features.Statistics;
using QSMPDLE.Web.Infrastructure;
using QSMPDLE.Web.Infrastructure.Persistence;
using QSMPDLE.Web.Themes;

// Prevent Npgsql from probing for GSSAPI (Kerberos) native libraries on
// managed hosts where libgssapi_krb5.so.2 may be missing. This must run
// before any DbContext or Npgsql connection is created.
Environment.SetEnvironmentVariable("NPGSQL_DISABLE_GSSAPI", "true");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalStorageServices();

builder.Services.AddMemoryCache();

builder.Services.AddDiagnostics();

builder.Services.AddMudServices();

builder.Services.AddThemes();

var connectionString = ResolveConnectionString(builder.Configuration);

// Pooled contexts avoid re-creating DbContext internals on every short-lived operation during traffic spikes.
builder.Services.AddPooledDbContextFactory<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddInfrastructure();

builder.Services.AddInternalCommunication();

builder.Services.AddGameplay();

builder.Services.AddSharingFeature();
builder.Services.AddStatistics();
builder.Services.AddArchive();
builder.Services.AddSitemap();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment();

        options.DisconnectedCircuitRetentionPeriod =
            TimeSpan.FromSeconds(builder.Configuration.GetValue("Circuits:DisconnectedRetentionSeconds", 30));

        options.DisconnectedCircuitMaxRetained = builder.Configuration.GetValue("Circuits:MaxDisconnected", 500);
    });

var app = builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Railway terminates TLS at its proxy; trust its forwarded scheme so HTTPS-only features and
// generated absolute URLs (sitemap) see the public request. The proxy address is not fixed.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Plain URLs (character images, fonts) keep a 30-day cache here; @Assets[...] URLs in App.razor
// are fingerprinted and fall through to MapStaticAssets, which serves them precompressed and immutable.
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

// Explicitly after UseStaticFiles: otherwise routing runs first, MapStaticAssets claims every
// wwwroot path, and plain URLs lose the 30-day cache above.
app.UseRouting();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapSitemap();

await app.MigrateDatabasesAsync();

app.Run();

// Locally ConnectionStrings:qsmpdle comes from the web project's user secrets;
// Railway supplies the libpq-style PG* variables instead.
static string ResolveConnectionString(IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("qsmpdle");
    if (!string.IsNullOrWhiteSpace(connectionString))
        return ApplyPoolSettings(new NpgsqlConnectionStringBuilder(connectionString), configuration);

    var host = configuration["PGHOST"];
    if (string.IsNullOrWhiteSpace(host))
    {
        throw new InvalidOperationException(
            "No database configured. Locally run: dotnet user-secrets set \"ConnectionStrings:qsmpdle\" \"<connection string>\" --project web. "
            + "In production set the PGHOST/PGPORT/PGDATABASE/PGUSER/PGPASSWORD variables.");
    }

    return ApplyPoolSettings(new NpgsqlConnectionStringBuilder
    {
        Host = host,
        Port = int.TryParse(configuration["PGPORT"], out var port) ? port : 5432,
        Database = configuration["PGDATABASE"],
        Username = configuration["PGUSER"],
        Password = configuration["PGPASSWORD"]
    }, configuration);
}

// Cap the pool below Postgres' max_connections (~100 on Railway) so a spike queues briefly
// in the app instead of exhausting the server; fail fast rather than hang circuits.
static string ApplyPoolSettings(NpgsqlConnectionStringBuilder builder, IConfiguration configuration)
{
    builder.MaxPoolSize = configuration.GetValue("Database:MaxPoolSize", 40);
    builder.Timeout = configuration.GetValue("Database:ConnectTimeoutSeconds", 15);
    builder.CommandTimeout = configuration.GetValue("Database:CommandTimeoutSeconds", 30);
    return builder.ConnectionString;
}

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
