using BlazorBootstrap;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;
using QSMPDLE.Web.Components;
using QSMPDLE.Web.Data;
using QSMPDLE.Web.Features.Characters;
using QSMPDLE.Web.Features.Gameplay;
using QSMPDLE.Web.Features.Sharing;
using QSMPDLE.Web.Features.Sharing.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalStorageServices();

builder.Services.AddMemoryCache();

builder.Services.AddMudServices();

var relativePath = builder.Configuration["Database:Path"];

var dbPath = Path.Combine(builder.Environment.ContentRootPath, relativePath!);

builder.Services.AddDbContext<QsmpdleDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddCharactersFeature();
builder.Services.AddGameplayFeature();
builder.Services.AddSharingFeature();

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

app.Run();
