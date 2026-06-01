var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://localhost:4201")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("Content-Disposition", "X-Pagination");
    });
});

// Add YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver()
    .ConfigureHttpClient((context, handler) =>
    {
        handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
    });

var app = builder.Build();

app.MapDefaultEndpoints();

// URL Rewriting Middleware to handle trailing slashes for microservice root endpoints
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (!string.IsNullOrEmpty(path) && !path.EndsWith("/"))
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 2 && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.Path = path + "/";
        }
        else if (segments.Length == 3 && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) && segments[1].Equals("v1", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.Path = path + "/";
        }
    }
    await next();
});

app.UseCors();

app.MapReverseProxy();

app.Run();
