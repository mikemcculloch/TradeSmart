using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using TradeSmart.Domain;
using TradeSmart.Domain.Interfaces.Proxies;
using TradeSmart.Infrastructure.Proxy;

namespace TradeSmart.Infrastructure;

/// <summary>Infrastructure layer DI registration.</summary>
public static class ServiceCollectionExtensions
{
	/// <summary>Registers infrastructure services (proxies, HTTP clients).</summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The application configuration.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection RegisterInfrastructureServices(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		// Proxies
		services.AddScoped<ITwelveDataProxy, TwelveDataProxy>();
		services.AddScoped<IClaudeProxy, ClaudeProxy>();
		services.AddScoped<IDiscordProxy, DiscordProxy>();
		services.AddScoped<IPaperTradingStateProxy, PaperTradingStateProxy>();

		// Twelve Data HTTP client with retry policy
		services.AddHttpClient(Constants.TWELVE_DATA_HTTP_CLIENT_NAME)
			.AddPolicyHandler(GetRetryPolicy());

		// Discord HTTP client
		services.AddHttpClient(Constants.DISCORD_HTTP_CLIENT_NAME)
			.AddPolicyHandler(GetRetryPolicy());

		// Claude HTTP client with API key header and retry policy
		services.AddHttpClient(Constants.CLAUDE_HTTP_CLIENT_NAME, (sp, client) =>
			{
				var config = sp.GetRequiredService<IConfiguration>();
				client.DefaultRequestHeaders.Add("x-api-key", config.GetClaudeApiKey());
				client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
			})
			.AddPolicyHandler(GetRetryPolicy());

		return services;
	}

	private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
	{
		return HttpPolicyExtensions
			.HandleTransientHttpError()
			.WaitAndRetryAsync(3, retryAttempt =>
				TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 100));
	}
}
