using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TradeSmart.Api.Dto;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;
using TradeSmart.Domain.Interfaces.Services;

namespace TradeSmart.Api.Controllers;

/// <summary>
/// Webhook endpoint for receiving TradingView strategy signals.
/// Every single incoming webhook triggers a Discord notification showing what was received and what decision was made.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class WebhooksController : ControllerBase
{
	private readonly IConfiguration _configuration;
	private readonly IDiscordProxy _discordProxy;
	private readonly ITradeHistoryProxy _tradeHistoryProxy;
	private readonly ILogger<WebhooksController> _logger;
	private readonly IMapper _mapper;
	private readonly ITradeAnalysisService _tradeAnalysisService;
	private readonly ITradeExecutionService _tradeExecutionService;

	public WebhooksController(
		ITradeExecutionService tradeExecutionService,
		ITradeAnalysisService tradeAnalysisService,
		IDiscordProxy discordProxy,
		ITradeHistoryProxy tradeHistoryProxy,
		IMapper mapper,
		ILogger<WebhooksController> logger,
		IConfiguration configuration)
	{
		_tradeExecutionService = tradeExecutionService;
		_tradeAnalysisService = tradeAnalysisService;
		_discordProxy = discordProxy;
		_tradeHistoryProxy = tradeHistoryProxy;
		_mapper = mapper;
		_logger = logger;
		_configuration = configuration;
	}

	/// <summary>
	/// Receives a TradingView strategy signal and executes immediately.
	/// Every webhook fires a Discord notification showing what came in and the decision.
	/// </summary>
	[HttpPost("tradingview")]
	[ProducesResponseType(typeof(TradeExecutionResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> ReceiveTradingViewAlertAsync(
		[FromBody] TradingViewAlertRequestDto request,
		CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
			return ValidationProblem(ModelState);

		// Validate webhook secret
		var expectedSecret = _configuration.GetWebhookSecret();
		if (!string.IsNullOrWhiteSpace(expectedSecret) && request.Secret != expectedSecret)
		{
			_logger.LogWarning("Webhook authentication failed for {Symbol}", request.Symbol);
			var badAlert = _mapper.Map<TradingViewAlert>(request);
			_ = NotifyDiscordSafe(badAlert, "REJECTED — AUTH FAILED", "Invalid webhook secret");
			_ = LogSignalSafe(badAlert, "REJECTED — AUTH FAILED", "Invalid webhook secret", executed: false);
			return Unauthorized(new { error = "Invalid webhook secret." });
		}

		var alert = _mapper.Map<TradingViewAlert>(request);

		_logger.LogInformation(
			"Received TradingView signal: {Type} {Symbol} {Direction} @ {Price}",
			alert.Type, alert.Symbol, alert.Direction ?? "n/a", alert.Price);

		// ── Close signals ───────────────────────────────────────────
		if (alert.IsClose)
		{
			return await HandleCloseSignalAsync(alert, cancellationToken).ConfigureAwait(false);
		}

		// ── Entry signals ───────────────────────────────────────────
		return await HandleEntrySignalAsync(alert, cancellationToken).ConfigureAwait(false);
	}

	private async Task<IActionResult> HandleEntrySignalAsync(TradingViewAlert alert, CancellationToken cancellationToken)
	{
		var executionResult = await _tradeExecutionService.ExecuteFromSignalAsync(alert, cancellationToken);

		if (executionResult.HasErrors)
		{
			var errorMsg = executionResult.Error!.Message;
			_logger.LogError("Execution failed for {Symbol}: [{Code}] {Error}",
				alert.Symbol, executionResult.Error.Code, errorMsg);

			_ = NotifyDiscordSafe(alert, "ERROR — EXECUTION FAILED", errorMsg);
			_ = LogSignalSafe(alert, "ERROR — EXECUTION FAILED", errorMsg, executed: false);
			return StatusCode(StatusCodes.Status500InternalServerError, new { error = errorMsg });
		}

		var result = executionResult.Result!;

		// Build decision string for Discord
		string decision;
		string? details;

		if (result.TradeOpened)
		{
			decision = $"TRADE OPENED — {result.Analysis.Direction}";
			details = $"Entry: ${result.Position?.EntryPrice:N2} | SL: ${result.Analysis.StopLoss:N2} | TP: ${result.Analysis.TakeProfit:N2}";
		}
		else
		{
			decision = "REJECTED";
			details = result.RejectionReason;
		}

		// Discord: signal received + decision (fires on EVERY webhook)
		_ = NotifyDiscordSafe(alert, decision, details);
		_ = LogSignalSafe(alert, decision, details, executed: result.TradeOpened);

		// Fire-and-forget: Claude audit for Discord insight (no trade gating)
		_ = Task.Run(() => _tradeAnalysisService.AuditAsync(alert, result, CancellationToken.None));

		return Ok(new TradeExecutionResultDto
		{
			TradeOpened = result.TradeOpened,
			Symbol = result.Analysis.Symbol,
			Direction = result.Analysis.Direction.ToString(),
			EntryPrice = result.Position?.EntryPrice,
			StopLoss = result.Analysis.StopLoss,
			TakeProfit = result.Analysis.TakeProfit,
			RejectionReason = result.RejectionReason
		});
	}

	private async Task<IActionResult> HandleCloseSignalAsync(TradingViewAlert alert, CancellationToken cancellationToken)
	{
		var closeResult = await _tradeExecutionService.CloseFromSignalAsync(alert, cancellationToken);

		if (closeResult.HasErrors)
		{
			var errorMsg = closeResult.Error!.Message;
			_logger.LogError("Close failed for {Symbol}: {Error}", alert.Symbol, errorMsg);

			_ = NotifyDiscordSafe(alert, "ERROR — CLOSE FAILED", errorMsg);
			_ = LogSignalSafe(alert, "ERROR — CLOSE FAILED", errorMsg, executed: false);
			return StatusCode(StatusCodes.Status500InternalServerError, new { error = errorMsg });
		}

		var closed = closeResult.Result;
		var decision = closed ? "POSITION CLOSED" : "NO POSITION FOUND";
		var details = closed
			? $"Exit signal: {alert.Message}"
			: $"No open position for {alert.Symbol} to close";

		_ = NotifyDiscordSafe(alert, decision, details);
		_ = LogSignalSafe(alert, decision, details, executed: closed);

		return Ok(new { closed, symbol = alert.Symbol, message = alert.Message });
	}

	/// <summary>Fire-and-forget Discord notification. Never throws.</summary>
	private async Task NotifyDiscordSafe(TradingViewAlert alert, string decision, string? details)
	{
		try
		{
			await _discordProxy.SendSignalReceivedNotificationAsync(alert, decision, details).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to send signal-received Discord notification for {Symbol}", alert.Symbol);
		}
	}

	/// <summary>Fire-and-forget signal log to SQLite. Never throws.</summary>
	private async Task LogSignalSafe(TradingViewAlert alert, string decision, string? details, bool executed)
	{
		try
		{
			var tradingMode = _configuration.GetValue<string>("Trading:Mode") ?? "Paper";
			var entry = new SignalLogEntry
			{
				Id = Guid.NewGuid().ToString("N"),
				ReceivedAt = DateTimeOffset.UtcNow,
				Type = alert.Type ?? "unknown",
				Symbol = alert.Symbol ?? "unknown",
				Exchange = alert.Exchange ?? "unknown",
				Direction = alert.Direction ?? "unknown",
				Price = alert.Price,
				Interval = alert.Interval ?? "unknown",
				StopLoss = alert.StopLoss,
				TakeProfit = alert.TakeProfit,
				Decision = decision,
				Details = details,
				Executed = executed,
				TradingMode = tradingMode
			};

			await _tradeHistoryProxy.LogSignalAsync(entry).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to log signal to SQLite for {Symbol}", alert.Symbol);
		}
	}
}
