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

	// ── Paper Trading ───────────────────────────────────────────────────

	/// <summary>Gets whether paper trading is enabled.</summary>
	public static bool GetPaperTradingEnabled(this IConfiguration configuration)
	{
		return configuration.GetValue("PaperTrading:Enabled", true);
	}

	/// <summary>Gets the paper trading initial balance.</summary>
	public static decimal GetPaperTradingInitialBalance(this IConfiguration configuration)
	{
		return configuration.GetValue("PaperTrading:InitialBalance",
			TradeSmart.Domain.Constants.PaperTrading.DEFAULT_INITIAL_BALANCE);
	}

	/// <summary>Gets the confidence threshold for auto-trading.</summary>
	public static int GetPaperTradingConfidenceThreshold(this IConfiguration configuration)
	{
		return configuration.GetValue("PaperTrading:ConfidenceThreshold",
			TradeSmart.Domain.Constants.PaperTrading.DEFAULT_CONFIDENCE_THRESHOLD);
	}

	/// <summary>Gets the max position size as a fraction of wallet (0.10 = 10%).</summary>
	public static decimal GetPaperTradingMaxPositionSizePercent(this IConfiguration configuration)
	{
		return configuration.GetValue("PaperTrading:MaxPositionSizePercent",
			TradeSmart.Domain.Constants.PaperTrading.DEFAULT_MAX_POSITION_SIZE_PERCENT);
	}

	/// <summary>Gets the max number of concurrent open positions.</summary>
	public static int GetPaperTradingMaxConcurrentPositions(this IConfiguration configuration)
	{
		return configuration.GetValue("PaperTrading:MaxConcurrentPositions",
			TradeSmart.Domain.Constants.PaperTrading.DEFAULT_MAX_CONCURRENT_POSITIONS);
	}

	/// <summary>Gets the leverage multiplier.</summary>
	public static decimal GetPaperTradingLeverage(this IConfiguration configuration)
	{
		return configuration.GetValue("PaperTrading:Leverage",
			TradeSmart.Domain.Constants.PaperTrading.DEFAULT_LEVERAGE);
	}

	/// <summary>Gets the max stop-loss percent (0.20 = 20%).</summary>
	public static decimal GetPaperTradingMaxStopLossPercent(this IConfiguration configuration)
	{
		return configuration.GetValue("PaperTrading:MaxStopLossPercent",
			TradeSmart.Domain.Constants.PaperTrading.DEFAULT_MAX_STOP_LOSS_PERCENT);
	}

	/// <summary>Gets the monitor poll interval in seconds.</summary>
	public static int GetPaperTradingMonitorIntervalSeconds(this IConfiguration configuration)
	{
		return configuration.GetValue("PaperTrading:MonitorIntervalSeconds",
			TradeSmart.Domain.Constants.PaperTrading.DEFAULT_MONITOR_INTERVAL_SECONDS);
	}

	/// <summary>Gets the paper trading state file path.</summary>
	public static string GetPaperTradingStateFilePath(this IConfiguration configuration)
	{
		return configuration.GetValue("PaperTrading:StateFilePath", "paper-trading-state.json")!;
	}
}
