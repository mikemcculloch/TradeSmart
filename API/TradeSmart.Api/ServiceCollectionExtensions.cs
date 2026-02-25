using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TradeSmart.Api;

/// <summary>API layer DI registration.</summary>
public static class ServiceCollectionExtensions
{
	/// <summary>Registers API-layer services.</summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The application configuration.</param>
	public static void RegisterApiServices(this IServiceCollection services, IConfiguration configuration)
	{
		// No API-layer-specific services at this time.
		// Hydrators, field registries, etc. would be registered here.
	}
}
