using BlazorBootstrap;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
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

Console.WriteLine(typeof(MudTheme).Assembly.Location);
Console.WriteLine(typeof(Modal).Assembly.Location);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
