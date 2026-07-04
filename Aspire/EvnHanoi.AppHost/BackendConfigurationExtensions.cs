using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

namespace EvnHanoi.AppHost;

internal static class BackendConfigurationExtensions
{
    internal static IResourceBuilder<ProjectResource> WithSharedInfrastructure(
        this IResourceBuilder<ProjectResource> project,
        IConfiguration configuration)
    {
        return project
            .WithEnvironment("ConnectionStrings__DefaultConnection", configuration["ConnectionStrings:DefaultConnection"])
            .WithEnvironment("ConnectionStrings__Redis", configuration["ConnectionStrings:Redis"])
            .WithEnvironment("RabbitMQ__Host", configuration["RabbitMQ:Host"])
            .WithEnvironment("RabbitMQ__Port", configuration["RabbitMQ:Port"])
            .WithEnvironment("RabbitMQ__VirtualHost", configuration["RabbitMQ:VirtualHost"])
            .WithEnvironment("RabbitMQ__Username", configuration["RabbitMQ:Username"])
            .WithEnvironment("RabbitMQ__Password", configuration["RabbitMQ:Password"])
            .WithEnvironment("Elasticsearch__Url", configuration["Elasticsearch:Url"])
            .WithEnvironment("Elasticsearch__Uri", configuration["Elasticsearch:Uri"] ?? configuration["Elasticsearch:Url"])
            .WithEnvironment("Jwt__Issuer", configuration["Jwt:Issuer"])
            .WithEnvironment("Jwt__Audience", configuration["Jwt:Audience"])
            .WithEnvironment("Jwt__Key", configuration["Jwt:Key"])
            .WithEnvironment("Internal__Token", configuration["Internal:Token"]);
    }

    internal static IResourceBuilder<ProjectResource> WithMinio(
        this IResourceBuilder<ProjectResource> project,
        IConfiguration configuration,
        bool useMinioSectionName = false)
    {
        var prefix = useMinioSectionName ? "Minio" : "MinIO";
        return project
            .WithEnvironment($"{prefix}__Endpoint", configuration[$"{prefix}:Endpoint"])
            .WithEnvironment($"{prefix}__AccessKey", configuration[$"{prefix}:AccessKey"])
            .WithEnvironment($"{prefix}__SecretKey", configuration[$"{prefix}:SecretKey"])
            .WithEnvironment($"{prefix}__UseSSL", configuration[$"{prefix}:UseSSL"]);
    }

    internal static IResourceBuilder<ProjectResource> WithServiceUrls(
        this IResourceBuilder<ProjectResource> project,
        IConfiguration configuration,
        params string[] serviceNames)
    {
        foreach (var name in serviceNames)
        {
            project.WithEnvironment($"Services__{name}", configuration[$"Services:{name}"]);
        }

        return project;
    }
}
