#pragma warning disable IDE0130

namespace Microsoft.Extensions.Configuration;

/// <summary>Configuration extension methods for TradeSmart settings.</summary>
public static class ConfigurationExtensions
{
	/// <summary>Gets the Claude API key.</summary>
	public static string GetClaudeApiKey(this IConfiguration configuration)
	{
		return configuration.GetValue<string>("Claude:ApiKey")
			?? throw new InvalidOperationException("Claude:ApiKey is not configured.");
	}

	/// <summary>Gets the Claude API base URL.</summary>
	public static string GetClaudeBaseUrl(this IConfiguration configuration, string? defaultValue = default)
	{
		return configuration.GetValue("Claude:BaseUrl", defaultValue ?? "https://api.anthropic.com");
	}

	/// <summary>Gets the Claude model to use for analysis.</summary>
	public static string GetClaudeModel(this IConfiguration configuration, string? defaultValue = default)
	{
		return configuration.GetValue("Claude:Model", defaultValue ?? "claude-sonnet-4-20250514");
	}

	/// <summary>Gets the Claude max tokens for responses.</summary>
	public static int GetClaudeMaxTokens(this IConfiguration configuration, int? defaultValue = default)
	{
		return configuration.GetValue("Claude:MaxTokens", defaultValue ?? 4096);
	}

	/// <summary>Gets the Twelve Data API key.</summary>
	public static string GetTwelveDataApiKey(this IConfiguration configuration)
	{
		return configuration.GetValue<string>("TwelveData:ApiKey")
			?? throw new InvalidOperationException("TwelveData:ApiKey is not configured.");
	}

	/// <summary>Gets the Twelve Data API base URL.</summary>
	public static string GetTwelveDataBaseUrl(this IConfiguration configuration, string? defaultValue = default)
	{
		return configuration.GetValue("TwelveData:BaseUrl", defaultValue ?? "https://api.twelvedata.com");
	}

	/// <summary>Gets the Discord webhook URL for trade notifications.</summary>
	public static string? GetDiscordWebhookUrl(this IConfiguration configuration, string? defaultValue = default)
	{
		return configuration.GetValue("Discord:WebhookUrl", defaultValue);
	}

	/// <summary>Gets the webhook shared secret for TradingView authentication.</summary>
	public static string? GetWebhookSecret(this IConfiguration configuration, string? defaultValue = default)
	{
		return configuration.GetValue("Webhook:Secret", defaultValue);
	}
}
