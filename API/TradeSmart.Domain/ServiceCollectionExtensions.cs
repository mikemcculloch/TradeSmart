using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeSmart.Domain.Interfaces.Services;
using TradeSmart.Domain.Services;

namespace TradeSmart.Domain;

/// <summary>Domain layer DI registration.</summary>
public static class ServiceCollectionExtensions
{
	/// <summary>Registers domain services.</summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The application configuration.</param>
	public static void RegisterDomainServices(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddScoped<ITradeAnalysisService, TradeAnalysisService>();
		services.AddSingleton<IPaperTradingService, PaperTradingService>();
		services.AddScoped<ITradeExecutionService, TradeExecutionService>();
	}
}
