using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Validation;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Embeddings;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Search;
using Overmind.Infrastructure.VectorSearch;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Overmind.Host;

public static class DependencyInjection
{
    public static IServiceCollection AddOvermind(this IServiceCollection services)
    {
        services.AddDbContext<OvermindDbContext>((provider, options) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var databasePath = configuration["Overmind:DatabasePath"] ?? OvermindPaths.DatabasePath;
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            options.UseSqlite($"Data Source={databasePath};Pooling=false");
        });

        services.AddScoped<FtsSearchService>();

        services.AddScoped<AppSettingsService>();
        services.AddScoped<EmbeddingProviderFactory>();
        services.AddScoped<QdrantSemanticSearch>();
        services.AddScoped<ISemanticSearch>(provider => provider.GetRequiredService<QdrantSemanticSearch>());
        services.AddScoped<EntryIndexingService>();
        services.AddScoped<ReindexService>();

        services.AddSingleton(PlatformDirectoryMap.Load());
        services.AddScoped<IMaterializer, SkillMaterializer>();

        services.AddSingleton<IValidator<CreateProjectRequest>, CreateProjectRequestValidator>();
        services.AddSingleton<IValidator<UpdateProjectRequest>, UpdateProjectRequestValidator>();
        services.AddSingleton<IValidator<CreateEntryRequest>, CreateEntryRequestValidator>();
        services.AddSingleton<IValidator<UpdateEntryRequest>, UpdateEntryRequestValidator>();
        services.AddSingleton<IValidator<CreateKitRequest>, CreateKitRequestValidator>();
        services.AddSingleton<IValidator<UpdateKitRequest>, UpdateKitRequestValidator>();

        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

        return services;
    }
}
