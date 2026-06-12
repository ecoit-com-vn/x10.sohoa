using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using FluentValidation.AspNetCore;
using System;
using System.Linq;

namespace EvnHanoi.Infrastructure.Security;

public static class ValidationExtensions
{
    public static IServiceCollection AddStructuredValidationErrors(this IServiceCollection services)
    {
        // Add FluentValidation auto-validation
        services.AddFluentValidationAutoValidation();
        
        // Scan and register all validators in the entry assembly and current domain
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        services.AddValidatorsFromAssemblies(assemblies);

        // Configure custom invalid model state response factory
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(ms => ms.Value != null && ms.Value.Errors.Count > 0)
                    .ToDictionary(
                        ms => {
                            var key = ms.Key;
                            var parts = key.Split('.');
                            var leafName = parts.Last();
                            return string.IsNullOrEmpty(leafName) ? leafName : char.ToLower(leafName[0]) + leafName.Substring(1);
                        },
                        ms => ms.Value!.Errors.First().ErrorMessage
                    );

                return new BadRequestObjectResult(new
                {
                    statusCode = 400,
                    message = "Dữ liệu đầu vào không hợp lệ.",
                    errors = errors
                });
            };
        });
        
        return services;
    }
}
