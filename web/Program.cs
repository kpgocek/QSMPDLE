using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using QSMPDLE.Web.Components;
using QSMPDLE.Web.Data;
using QSMPDLE.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalStorageServices();

builder.Services.AddMemoryCache();

builder.Services.AddMudServices();

var relativePath = builder.Configuration["Database:Path"];

var dbPath = Path.Combine(builder.Environment.ContentRootPath, relativePath!);

builder.Services.AddDbContext<QsmpdleDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<IMemberRepository, MemberRepository>();
builder.Services.AddScoped<IGameStateStore, LocalStorageGameStateStore>();
builder.Services.AddScoped<IPlayerStatsStore, LocalStoragePlayerStatsStore>();
builder.Services.AddScoped<IGameService, GameService>();

builder.Services.AddScoped<IShareService, ShareService>();
builder.Services.AddSingleton<IShareTextBuilder, ShareTextBuilder>();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

Console.WriteLine(typeof(Program).Assembly.Location);
Console.WriteLine(AppContext.BaseDirectory);

Console.WriteLine(File.Exists(
    Path.Combine(
        AppContext.BaseDirectory,
        "QSMPDLE.Web.staticwebassets.endpoints.json")));

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
