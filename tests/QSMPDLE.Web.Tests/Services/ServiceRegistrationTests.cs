using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using QSMPDLE.Web.Diagnostics;
using QSMPDLE.Web.Features.Archive;
using QSMPDLE.Web.Features.Gameplay;
using QSMPDLE.Web.Features.Sitemap;
using QSMPDLE.Web.Features.Statistics;
using QSMPDLE.Web.Infrastructure;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Tests.Services;

public sealed class ServiceRegistrationTests
{
    [Fact]
    public async Task ProgramRegistrations_BuildInDevelopment_ValidateEveryService()
    {
        // Mirrors Program.cs. Development builds validate scopes and every descriptor, so a registration
        // removed together with a feature (as RuntimeCounters once was) fails here instead of at startup.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });

        builder.Services.AddLocalStorageServices();
        builder.Services.AddMemoryCache();
        builder.Services.AddDiagnostics();
        builder.Services.AddMudServices();
        builder.Services.AddPooledDbContextFactory<ApplicationDbContext>(options => options.UseNpgsql("Host=localhost"));
        builder.Services.AddInfrastructure();
        builder.Services.AddGameplay();
        builder.Services.AddStatistics();
        builder.Services.AddArchive();
        builder.Services.AddSitemap();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();

        WebApplication? app = null;
        var build = () => app = builder.Build();

        build.Should().NotThrow();
        await app!.DisposeAsync();
    }
}
