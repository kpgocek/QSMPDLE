using Microsoft.Extensions.DependencyInjection;
using QSMPDLE.Web.Features.Characters.Repositories;

namespace QSMPDLE.Web.Features.Characters;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCharactersFeature(this IServiceCollection services)
    {
        services.AddScoped<ICharacterRepository, CharacterRepository>();
        return services;
    }
}
