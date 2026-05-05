using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace PropertyManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var asm = Assembly.GetExecutingAssembly();
        services.AddAutoMapper(asm);
        services.AddValidatorsFromAssembly(asm);
        return services;
    }
}
