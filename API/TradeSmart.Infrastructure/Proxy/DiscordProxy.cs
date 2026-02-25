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

	/// <inheritdoc />
	public async Task<ProxyResponse<bool>> SendTradeOpenedNotificationAsync(
		PaperPosition position,
		PaperWallet wallet,
		CancellationToken cancellationToken = default)
	{
		var webhookUrl = _configuration.GetDiscordWebhookUrl();

		if (string.IsNullOrWhiteSpace(webhookUrl))
		{
			return ProxyResponse<bool>.Success(false);
		}

		try
		{
			var payload = BuildTradeOpenedPayload(position, wallet);
			var json = JsonConvert.SerializeObject(payload);
			var content = new StringContent(json, Encoding.UTF8, "application/json");

			var client = _httpClientFactory.CreateClient(Constants.DISCORD_HTTP_CLIENT_NAME);
			var response = await client.PostAsync(webhookUrl, content, cancellationToken)
				.ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				_logger.LogWarning(
					"Discord webhook returned {StatusCode} for trade opened: {Body}",
					(int)response.StatusCode,
					body);

				return ProxyResponse<bool>.CreateError(
					Constants.ErrorCodes.DISCORD_NOTIFICATION_ERROR,
					$"Discord webhook returned {(int)response.StatusCode}.");
			}

			_logger.LogInformation("Discord trade-opened notification sent for {Symbol}", position.Symbol);
			return ProxyResponse<bool>.Success(true);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to send trade-opened Discord notification for {Symbol}", position.Symbol);
			return ProxyResponse<bool>.CreateError(
				Constants.ErrorCodes.DISCORD_NOTIFICATION_ERROR,
				$"Discord notification failed: {ex.Message}");
		}
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<bool>> SendTradeClosedNotificationAsync(
		PaperPosition closedPosition,
		PaperWallet wallet,
		CancellationToken cancellationToken = default)
	{
		var webhookUrl = _configuration.GetDiscordWebhookUrl();

		if (string.IsNullOrWhiteSpace(webhookUrl))
		{
			return ProxyResponse<bool>.Success(false);
		}

		try
		{
			var payload = BuildTradeClosedPayload(closedPosition, wallet);
			var json = JsonConvert.SerializeObject(payload);
			var content = new StringContent(json, Encoding.UTF8, "application/json");

			var client = _httpClientFactory.CreateClient(Constants.DISCORD_HTTP_CLIENT_NAME);
			var response = await client.PostAsync(webhookUrl, content, cancellationToken)
				.ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				_logger.LogWarning(
					"Discord webhook returned {StatusCode} for trade closed: {Body}",
					(int)response.StatusCode,
					body);

				return ProxyResponse<bool>.CreateError(
					Constants.ErrorCodes.DISCORD_NOTIFICATION_ERROR,
					$"Discord webhook returned {(int)response.StatusCode}.");
			}

			_logger.LogInformation("Discord trade-closed notification sent for {Symbol}", closedPosition.Symbol);
			return ProxyResponse<bool>.Success(true);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to send trade-closed Discord notification for {Symbol}", closedPosition.Symbol);
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

	private static object BuildTradeOpenedPayload(PaperPosition position, PaperWallet wallet)
	{
		var directionEmoji = position.Direction == TradeDirection.Long
			? "\U0001F7E2"  // 🟢
			: "\U0001F534"; // 🔴

		var color = position.Direction == TradeDirection.Long ? 3066993 : 15158332;
		var notional = position.PositionSizeUsd * position.Leverage;

		var fields = new List<object>
		{
			new { name = "Direction", value = $"{directionEmoji} **{position.Direction}**", inline = true },
			new { name = "Entry Price", value = $"${position.EntryPrice:N2}", inline = true },
			new { name = "Confidence", value = $"**{position.Confidence}%**", inline = true },
			new { name = "Position Size", value = $"${position.PositionSizeUsd:N2}", inline = true },
			new { name = "Leverage", value = $"{position.Leverage}x", inline = true },
			new { name = "Notional", value = $"${notional:N2}", inline = true },
			new { name = "Stop Loss", value = $"${position.StopLoss:N2}", inline = true },
			new { name = "Take Profit", value = $"${position.TakeProfit:N2}", inline = true },
			new { name = "Wallet Balance", value = $"${wallet.AvailableBalance:N2}", inline = true }
		};

		if (!string.IsNullOrWhiteSpace(position.Reasoning))
		{
			var reasoning = position.Reasoning.Length > 1000
				? position.Reasoning[..997] + "..."
				: position.Reasoning;

			fields.Add(new { name = "Analysis", value = reasoning, inline = false });
		}

		var embed = new
		{
			title = $"{directionEmoji} PAPER TRADE OPENED \u2014 {position.Symbol}",
			color,
			fields,
			footer = new { text = $"TradeSmart Paper Trading | {position.OpenedAt:yyyy-MM-dd HH:mm:ss} UTC" },
			timestamp = position.OpenedAt.ToString("o")
		};

		return new
		{
			username = "TradeSmart",
			embeds = new[] { embed }
		};
	}

	private static object BuildTradeClosedPayload(PaperPosition position, PaperWallet wallet)
	{
		var pnl = position.RealizedPnl ?? 0m;
		var isProfit = pnl >= 0;
		var pnlEmoji = isProfit ? "\U0001F7E2" : "\U0001F534"; // 🟢 or 🔴
		var color = isProfit ? 3066993 : 15158332;
		var pnlPercent = position.PositionSizeUsd != 0
			? pnl / position.PositionSizeUsd * 100
			: 0;

		var duration = (position.ClosedAt ?? DateTimeOffset.UtcNow) - position.OpenedAt;
		var durationText = duration.TotalHours >= 1
			? $"{(int)duration.TotalHours}h {duration.Minutes}m"
			: $"{(int)duration.TotalMinutes}m";

		var winRate = wallet.TotalTrades > 0
			? (decimal)wallet.WinningTrades / wallet.TotalTrades * 100
			: 0;

		var directionEmoji = position.Direction == TradeDirection.Long
			? "\U0001F7E2"
			: "\U0001F534";

		var fields = new List<object>
		{
			new { name = "Direction", value = $"{directionEmoji} **{position.Direction}**", inline = true },
			new { name = "Entry", value = $"${position.EntryPrice:N2}", inline = true },
			new { name = "Exit", value = $"${position.ExitPrice:N2}", inline = true },
			new { name = "PnL", value = $"{(pnl >= 0 ? "+" : "")}{pnl:N2} USD", inline = true },
			new { name = "PnL %", value = $"{(pnlPercent >= 0 ? "+" : "")}{pnlPercent:N2}%", inline = true },
			new { name = "Close Reason", value = position.CloseReason ?? "Unknown", inline = true },
			new { name = "Duration", value = durationText, inline = true },
			new { name = "Wallet Balance", value = $"${wallet.AvailableBalance:N2}", inline = true },
			new { name = "Total PnL", value = $"${wallet.TotalRealizedPnl:N2}", inline = true },
			new { name = "Win Rate", value = $"{winRate:N1}% ({wallet.WinningTrades}W/{wallet.LosingTrades}L)", inline = true }
		};

		var embed = new
		{
			title = $"{pnlEmoji} PAPER TRADE CLOSED \u2014 {position.Symbol} ({position.CloseReason})",
			color,
			fields,
			footer = new { text = $"TradeSmart Paper Trading | {(position.ClosedAt ?? DateTimeOffset.UtcNow):yyyy-MM-dd HH:mm:ss} UTC" },
			timestamp = (position.ClosedAt ?? DateTimeOffset.UtcNow).ToString("o")
		};

		return new
		{
			username = "TradeSmart",
			embeds = new[] { embed }
		};
	}
}
