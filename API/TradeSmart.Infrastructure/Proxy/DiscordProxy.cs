using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradeSmart.Domain;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;

namespace TradeSmart.Infrastructure.Proxy;

/// <summary>Sends trade notifications to Discord via webhook.</summary>
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

	// ── Shared HTTP helper ──────────────────────────────────────────────

	private async Task<ProxyResponse<bool>> PostPayloadAsync(
		object payload, string symbol, string notificationType, CancellationToken cancellationToken)
	{
		var webhookUrl = _configuration.GetDiscordWebhookUrl();
		if (string.IsNullOrWhiteSpace(webhookUrl))
		{
			_logger.LogDebug("Discord webhook URL not configured — skipping {Type} for {Symbol}", notificationType, symbol);
			return ProxyResponse<bool>.Success(false);
		}

		try
		{
			var json = JsonConvert.SerializeObject(payload);
			var content = new StringContent(json, Encoding.UTF8, "application/json");

			var client = _httpClientFactory.CreateClient(Constants.DISCORD_HTTP_CLIENT_NAME);
			var response = await client.PostAsync(webhookUrl, content, cancellationToken).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				_logger.LogWarning("Discord {Type} returned {StatusCode}: {Body}", notificationType, (int)response.StatusCode, body);
				return ProxyResponse<bool>.CreateError(
					Constants.ErrorCodes.DISCORD_NOTIFICATION_ERROR,
					$"Discord webhook returned {(int)response.StatusCode}.");
			}

			_logger.LogInformation("Discord {Type} notification sent for {Symbol}", notificationType, symbol);
			return ProxyResponse<bool>.Success(true);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to send Discord {Type} notification for {Symbol}", notificationType, symbol);
			return ProxyResponse<bool>.CreateError(
				Constants.ErrorCodes.DISCORD_NOTIFICATION_ERROR,
				$"Discord notification failed: {ex.Message}");
		}
	}

	// ════════════════════════════════════════════════════════════════════
	//  Signal Received — fires on EVERY incoming webhook
	// ════════════════════════════════════════════════════════════════════

	/// <inheritdoc />
	public Task<ProxyResponse<bool>> SendSignalReceivedNotificationAsync(
		TradingViewAlert alert,
		string decision,
		string? details = null,
		CancellationToken cancellationToken = default)
	{
		var payload = BuildSignalReceivedPayload(alert, decision, details);
		return PostPayloadAsync(payload, alert.Symbol, "signal-received", cancellationToken);
	}

	// ════════════════════════════════════════════════════════════════════
	//  Trade Analysis (Claude audit)
	// ════════════════════════════════════════════════════════════════════

	/// <inheritdoc />
	public Task<ProxyResponse<bool>> SendTradeNotificationAsync(
		TradingViewAlert alert,
		TradeAnalysis analysis,
		CancellationToken cancellationToken = default)
	{
		var payload = BuildAnalysisPayload(alert, analysis);
		return PostPayloadAsync(payload, analysis.Symbol, "analysis", cancellationToken);
	}

	// ════════════════════════════════════════════════════════════════════
	//  Trade Opened
	// ════════════════════════════════════════════════════════════════════

	/// <inheritdoc />
	public Task<ProxyResponse<bool>> SendTradeOpenedNotificationAsync(
		PaperPosition position,
		PaperWallet wallet,
		CancellationToken cancellationToken = default)
	{
		var payload = BuildTradeOpenedPayload(position, wallet);
		return PostPayloadAsync(payload, position.Symbol, "trade-opened", cancellationToken);
	}

	// ════════════════════════════════════════════════════════════════════
	//  Trade Closed
	// ════════════════════════════════════════════════════════════════════

	/// <inheritdoc />
	public Task<ProxyResponse<bool>> SendTradeClosedNotificationAsync(
		PaperPosition closedPosition,
		PaperWallet wallet,
		CancellationToken cancellationToken = default)
	{
		var payload = BuildTradeClosedPayload(closedPosition, wallet);
		return PostPayloadAsync(payload, closedPosition.Symbol, "trade-closed", cancellationToken);
	}

	// ── Payload Builders ────────────────────────────────────────────────

	private static object BuildSignalReceivedPayload(TradingViewAlert alert, string decision, string? details)
	{
		// Color: blue for entry, orange for close, grey for unknown
		var isEntry = alert.IsEntry;
		var isClose = alert.IsClose;
		var color = isEntry ? 3447003 : isClose ? 15105570 : 9807270; // blue / orange / grey

		var typeEmoji = isEntry ? "\U0001F4E8" : isClose ? "\U0001F4E4" : "\u2753"; // 📨 / 📤 / ❓
		var decisionEmoji = decision.Contains("OPENED", StringComparison.OrdinalIgnoreCase)
			? "\u2705" // ✅
			: decision.Contains("CLOSED", StringComparison.OrdinalIgnoreCase)
				? "\u2705"
				: decision.Contains("REJECTED", StringComparison.OrdinalIgnoreCase) || decision.Contains("ERROR", StringComparison.OrdinalIgnoreCase)
					? "\u274C" // ❌
					: "\u2139\uFE0F"; // ℹ️

		var fields = new List<object>
		{
			new { name = "Type", value = $"{typeEmoji} **{alert.Type.ToUpperInvariant()}**", inline = true },
			new { name = "Symbol", value = $"**{alert.Symbol}**", inline = true },
			new { name = "Price", value = $"${alert.Price:N2}", inline = true }
		};

		if (!string.IsNullOrWhiteSpace(alert.Direction))
			fields.Add(new { name = "Direction", value = $"**{alert.Direction}**", inline = true });

		if (alert.StopLoss.HasValue)
			fields.Add(new { name = "Stop Loss", value = $"${alert.StopLoss:N2}", inline = true });

		if (alert.TakeProfit.HasValue)
			fields.Add(new { name = "Take Profit", value = $"${alert.TakeProfit:N2}", inline = true });

		if (!string.IsNullOrWhiteSpace(alert.Interval))
			fields.Add(new { name = "Interval", value = alert.Interval, inline = true });

		if (!string.IsNullOrWhiteSpace(alert.Exchange))
			fields.Add(new { name = "Exchange", value = alert.Exchange, inline = true });

		fields.Add(new { name = $"{decisionEmoji} Decision", value = $"**{decision}**", inline = false });

		if (!string.IsNullOrWhiteSpace(details))
		{
			var truncated = details.Length > 1000 ? details[..997] + "..." : details;
			fields.Add(new { name = "Details", value = truncated, inline = false });
		}

		var embed = new
		{
			title = $"{typeEmoji} SIGNAL RECEIVED \u2014 {alert.Symbol}",
			color,
			fields,
			footer = new { text = $"TradeSmart | {alert.ReceivedAt:yyyy-MM-dd HH:mm:ss} UTC" },
			timestamp = alert.ReceivedAt.ToString("o")
		};

		return new { username = "TradeSmart", embeds = new[] { embed } };
	}

	private static object BuildAnalysisPayload(TradingViewAlert alert, TradeAnalysis analysis)
	{
		var directionEmoji = analysis.Direction switch
		{
			TradeDirection.Long => "\U0001F7E2",
			TradeDirection.Short => "\U0001F534",
			_ => "\U000026AA"
		};

		var color = analysis.Direction switch
		{
			TradeDirection.Long => 3066993,
			TradeDirection.Short => 15158332,
			_ => 9807270
		};

		var fields = new List<object>
		{
			new { name = "Direction", value = $"{directionEmoji} **{analysis.Direction}**", inline = true },
			new { name = "Confidence", value = $"**{analysis.Confidence}%**", inline = true },
			new { name = "Risk/Reward", value = analysis.RiskRewardRatio ?? "N/A", inline = true }
		};

		if (analysis.EntryPrice.HasValue)
			fields.Add(new { name = "Entry", value = $"${analysis.EntryPrice:N2}", inline = true });
		if (analysis.StopLoss.HasValue)
			fields.Add(new { name = "Stop Loss", value = $"${analysis.StopLoss:N2}", inline = true });
		if (analysis.TakeProfit.HasValue)
			fields.Add(new { name = "Take Profit", value = $"${analysis.TakeProfit:N2}", inline = true });

		var reasoning = analysis.Reasoning.Length > 1000 ? analysis.Reasoning[..997] + "..." : analysis.Reasoning;
		fields.Add(new { name = "Analysis", value = reasoning, inline = false });

		var embed = new
		{
			title = $"\U0001F916 CLAUDE AUDIT \u2014 {analysis.Symbol}",
			color,
			fields,
			footer = new { text = $"TradeSmart AI Audit | {analysis.AnalyzedAt:yyyy-MM-dd HH:mm:ss} UTC" },
			timestamp = analysis.AnalyzedAt.ToString("o")
		};

		return new { username = "TradeSmart", embeds = new[] { embed } };
	}

	private static object BuildTradeOpenedPayload(PaperPosition position, PaperWallet wallet)
	{
		var directionEmoji = position.Direction == TradeDirection.Long ? "\U0001F7E2" : "\U0001F534";
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
			var reasoning = position.Reasoning.Length > 1000 ? position.Reasoning[..997] + "..." : position.Reasoning;
			fields.Add(new { name = "Analysis", value = reasoning, inline = false });
		}

		var embed = new
		{
			title = $"{directionEmoji} TRADE OPENED \u2014 {position.Symbol}",
			color,
			fields,
			footer = new { text = $"TradeSmart | {position.OpenedAt:yyyy-MM-dd HH:mm:ss} UTC" },
			timestamp = position.OpenedAt.ToString("o")
		};

		return new { username = "TradeSmart", embeds = new[] { embed } };
	}

	private static object BuildTradeClosedPayload(PaperPosition position, PaperWallet wallet)
	{
		var pnl = position.RealizedPnl ?? 0m;
		var isProfit = pnl >= 0;
		var pnlEmoji = isProfit ? "\U0001F7E2" : "\U0001F534";
		var color = isProfit ? 3066993 : 15158332;
		var pnlPercent = position.PositionSizeUsd != 0 ? pnl / position.PositionSizeUsd * 100 : 0;

		var duration = (position.ClosedAt ?? DateTimeOffset.UtcNow) - position.OpenedAt;
		var durationText = duration.TotalHours >= 1
			? $"{(int)duration.TotalHours}h {duration.Minutes}m"
			: $"{(int)duration.TotalMinutes}m";

		var winRate = wallet.TotalTrades > 0
			? (decimal)wallet.WinningTrades / wallet.TotalTrades * 100
			: 0;

		var directionEmoji = position.Direction == TradeDirection.Long ? "\U0001F7E2" : "\U0001F534";

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
			title = $"{pnlEmoji} TRADE CLOSED \u2014 {position.Symbol} ({position.CloseReason})",
			color,
			fields,
			footer = new { text = $"TradeSmart | {(position.ClosedAt ?? DateTimeOffset.UtcNow):yyyy-MM-dd HH:mm:ss} UTC" },
			timestamp = (position.ClosedAt ?? DateTimeOffset.UtcNow).ToString("o")
		};

		return new { username = "TradeSmart", embeds = new[] { embed } };
	}
}
