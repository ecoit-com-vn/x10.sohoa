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
            .WithEnvIfSet("ConnectionStrings__DefaultConnection", configuration["ConnectionStrings:DefaultConnection"])
            .WithEnvIfSet("ConnectionStrings__Redis", configuration["ConnectionStrings:Redis"])
            .WithEnvIfSet("RabbitMQ__Host", configuration["RabbitMQ:Host"])
            .WithEnvIfSet("RabbitMQ__Port", configuration["RabbitMQ:Port"])
            .WithEnvIfSet("RabbitMQ__VirtualHost", configuration["RabbitMQ:VirtualHost"])
            .WithEnvIfSet("RabbitMQ__Username", configuration["RabbitMQ:Username"])
            .WithEnvIfSet("RabbitMQ__Password", configuration["RabbitMQ:Password"])
            .WithEnvIfSet("Elasticsearch__Url", configuration["Elasticsearch:Url"])
            .WithEnvIfSet("Jwt__Issuer", configuration["Jwt:Issuer"])
            .WithEnvIfSet("Jwt__Audience", configuration["Jwt:Audience"])
            .WithEnvIfSet("Jwt__Key", configuration["Jwt:Key"])
            .WithEnvIfSet("Internal__Token", configuration["Internal:Token"]);
    }

    internal static IResourceBuilder<ProjectResource> WithMinio(
        this IResourceBuilder<ProjectResource> project,
        IConfiguration configuration)
    {
        return project
            .WithEnvIfSet("MinIO__Endpoint", configuration["MinIO:Endpoint"])
            .WithEnvIfSet("MinIO__AccessKey", configuration["MinIO:AccessKey"])
            .WithEnvIfSet("MinIO__SecretKey", configuration["MinIO:SecretKey"])
            .WithEnvIfSet("MinIO__UseSSL", configuration["MinIO:UseSSL"]);
    }

    internal static IResourceBuilder<ProjectResource> WithServiceUrls(
        this IResourceBuilder<ProjectResource> project,
        IConfiguration configuration,
        params string[] serviceNames)
    {
        foreach (var name in serviceNames)
        {
            project.WithEnvIfSet($"Services__{name}", configuration[$"Services:{name}"]);
        }

        return project;
    }

    private static IResourceBuilder<ProjectResource> WithEnvIfSet(
        this IResourceBuilder<ProjectResource> project,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            project.WithEnvironment(name, value);

        return project;
    }
}
