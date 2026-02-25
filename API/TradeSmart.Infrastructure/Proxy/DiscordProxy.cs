using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradeSmart.Domain;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;

namespace TradeSmart.Infrastructure.Proxy;

/// <summary>Sends trade analysis notifications to Discord via webhook.</summary>
public sealed class DiscordProxy : IDiscordProxy
{
	private readonly IConfiguration _configuration;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ILogger<DiscordProxy> _logger;

	public DiscordProxy(
		IHttpClientFactory httpClientFactory,
		IConfiguration configuration,
		ILogger<DiscordProxy> logger)
	{
		_httpClientFactory = httpClientFactory;
		_configuration = configuration;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<bool>> SendTradeNotificationAsync(
		TradingViewAlert alert,
		TradeAnalysis analysis,
		CancellationToken cancellationToken = default)
	{
		var webhookUrl = _configuration.GetDiscordWebhookUrl();

		if (string.IsNullOrWhiteSpace(webhookUrl))
		{
			_logger.LogWarning("Discord webhook URL is not configured — skipping notification");
			return ProxyResponse<bool>.Success(false);
		}

		try
		{
			var payload = BuildPayload(alert, analysis);
			var json = JsonConvert.SerializeObject(payload);
			var content = new StringContent(json, Encoding.UTF8, "application/json");

			var client = _httpClientFactory.CreateClient(Constants.DISCORD_HTTP_CLIENT_NAME);
			var response = await client.PostAsync(webhookUrl, content, cancellationToken)
				.ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				_logger.LogWarning(
					"Discord webhook returned {StatusCode}: {Body}",
					(int)response.StatusCode,
					body);

				return ProxyResponse<bool>.CreateError(
					Constants.ErrorCodes.DISCORD_NOTIFICATION_ERROR,
					$"Discord webhook returned {(int)response.StatusCode}.");
			}

			_logger.LogInformation("Discord notification sent for {Symbol}", analysis.Symbol);
			return ProxyResponse<bool>.Success(true);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to send Discord notification for {Symbol}", analysis.Symbol);
			return ProxyResponse<bool>.CreateError(
				Constants.ErrorCodes.DISCORD_NOTIFICATION_ERROR,
				$"Discord notification failed: {ex.Message}");
		}
	}

	private static object BuildPayload(TradingViewAlert alert, TradeAnalysis analysis)
	{
		var directionEmoji = analysis.Direction switch
		{
			TradeDirection.Long => "\U0001F7E2",  // 🟢
			TradeDirection.Short => "\U0001F534", // 🔴
			_ => "\U000026AA"                     // ⚪
		};

		var color = analysis.Direction switch
		{
			TradeDirection.Long => 3066993,   // green
			TradeDirection.Short => 15158332, // red
			_ => 9807270                      // grey
		};

		var fields = new List<object>
		{
			new { name = "Direction", value = $"{directionEmoji} **{analysis.Direction}**", inline = true },
			new { name = "Confidence", value = $"**{analysis.Confidence}%**", inline = true },
			new { name = "Risk/Reward", value = analysis.RiskRewardRatio ?? "N/A", inline = true }
		};

		if (analysis.EntryPrice.HasValue)
		{
			fields.Add(new { name = "Entry", value = $"${analysis.EntryPrice:N2}", inline = true });
		}

		if (analysis.StopLoss.HasValue)
		{
			fields.Add(new { name = "Stop Loss", value = $"${analysis.StopLoss:N2}", inline = true });
		}

		if (analysis.TakeProfit.HasValue)
		{
			fields.Add(new { name = "Take Profit", value = $"${analysis.TakeProfit:N2}", inline = true });
		}

		// Truncate reasoning to fit Discord embed field limit (1024 chars)
		var reasoning = analysis.Reasoning.Length > 1000
			? analysis.Reasoning[..997] + "..."
			: analysis.Reasoning;

		fields.Add(new { name = "Analysis", value = reasoning, inline = false });

		var embed = new
		{
			title = $"{directionEmoji} {analysis.Symbol} — Trade Signal",
			color,
			fields,
			footer = new { text = $"TradeSmart | {analysis.AnalyzedAt:yyyy-MM-dd HH:mm:ss} UTC" },
			timestamp = analysis.AnalyzedAt.ToString("o")
		};

		return new
		{
			username = "TradeSmart",
			embeds = new[] { embed }
		};
	}
}
