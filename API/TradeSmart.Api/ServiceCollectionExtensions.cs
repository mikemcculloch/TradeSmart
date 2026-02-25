using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeSmart.Api.HostedServices;

namespace TradeSmart.Api;

/// <summary>API layer DI registration.</summary>
public static class ServiceCollectionExtensions
{
	/// <summary>Registers API-layer services.</summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The application configuration.</param>
	public static void RegisterApiServices(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddHostedService<TradeMonitorService>();
	}
}
