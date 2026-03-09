using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TradeSmart.Api.Dto;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;
using TradeSmart.Domain.Interfaces.Services;

namespace TradeSmart.Api.Controllers;

/// <summary>
/// Signal-only trading endpoint for strategies that manage entries/exits via TradingView alerts.
/// No Claude AI, no TwelveData, no SL/TP — purely signal-driven.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class SignalsController : ControllerBase
{
	private readonly IConfiguration _configuration;
	private readonly IDiscordProxy _discordProxy;
	private readonly ITradeHistoryProxy _tradeHistoryProxy;
	private readonly ILogger<SignalsController> _logger;
	private readonly IMapper _mapper;
	private readonly ITradeExecutionService _tradeExecutionService;

	public SignalsController(
		ITradeExecutionService tradeExecutionService,
		IDiscordProxy discordProxy,
		ITradeHistoryProxy tradeHistoryProxy,
		IMapper mapper,
		ILogger<SignalsController> logger,
		IConfiguration configuration)
	{
		_tradeExecutionService = tradeExecutionService;
		_discordProxy = discordProxy;
		_tradeHistoryProxy = tradeHistoryProxy;
		_mapper = mapper;
		_logger = logger;
		_configuration = configuration;
	}

	/// <summary>
	/// Receives a signal-only strategy alert from TradingView and executes immediately.
	/// No SL/TP — exits are purely signal-driven.
	/// </summary>
	[HttpPost("strategy")]
	[ProducesResponseType(typeof(TradeExecutionResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> ReceiveStrategySignalAsync(
		[FromBody] StrategySignalRequestDto request,
		CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
			return ValidationProblem(ModelState);

		// Validate webhook secret
		var expectedSecret = _configuration.GetWebhookSecret();
		if (!string.IsNullOrWhiteSpace(expectedSecret) && request.Secret != expectedSecret)
		{
			_logger.LogWarning("Signal authentication failed for {Symbol}", request.Symbol);
			var badAlert = _mapper.Map<TradingViewAlert>(request);
			_ = NotifyDiscordSafe(badAlert, "REJECTED — AUTH FAILED", "Invalid webhook secret");
			_ = LogSignalSafe(badAlert, "REJECTED — AUTH FAILED", "Invalid webhook secret", executed: false);
			return Unauthorized(new { error = "Invalid webhook secret." });
		}

		var alert = _mapper.Map<TradingViewAlert>(request);

		_logger.LogInformation(
			"Received strategy signal: {Type} {Symbol} {Direction} @ {Price}",
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
		var executionResult = await _tradeExecutionService.ExecuteSignalTradeAsync(alert, cancellationToken);

		if (executionResult.HasErrors)
		{
			var errorMsg = executionResult.Error!.Message;
			_logger.LogError("Signal execution failed for {Symbol}: [{Code}] {Error}",
				alert.Symbol, executionResult.Error.Code, errorMsg);

			_ = NotifyDiscordSafe(alert, "ERROR — EXECUTION FAILED", errorMsg);
			_ = LogSignalSafe(alert, "ERROR — EXECUTION FAILED", errorMsg, executed: false);
			return StatusCode(StatusCodes.Status500InternalServerError, new { error = errorMsg });
		}

		var result = executionResult.Result!;

		string decision;
		string? details;

		if (result.TradeOpened)
		{
			decision = $"SIGNAL TRADE OPENED — {result.Analysis.Direction}";
			details = $"Entry: ${result.Position?.EntryPrice:N2} | Size: ${result.Position?.PositionSizeUsd:N2} | No SL/TP (signal-only)";
		}
		else
		{
			decision = "REJECTED";
			details = result.RejectionReason;
		}

		_ = NotifyDiscordSafe(alert, decision, details);
		_ = LogSignalSafe(alert, decision, details, executed: result.TradeOpened);

		return Ok(new TradeExecutionResultDto
		{
			TradeOpened = result.TradeOpened,
			Symbol = result.Analysis.Symbol,
			Direction = result.Analysis.Direction.ToString(),
			EntryPrice = result.Position?.EntryPrice,
			RejectionReason = result.RejectionReason
		});
	}

	private async Task<IActionResult> HandleCloseSignalAsync(TradingViewAlert alert, CancellationToken cancellationToken)
	{
		var closeResult = await _tradeExecutionService.CloseFromSignalAsync(alert, cancellationToken);

		if (closeResult.HasErrors)
		{
			var errorMsg = closeResult.Error!.Message;
			_logger.LogError("Signal close failed for {Symbol}: {Error}", alert.Symbol, errorMsg);

			_ = NotifyDiscordSafe(alert, "ERROR — CLOSE FAILED", errorMsg);
			_ = LogSignalSafe(alert, "ERROR — CLOSE FAILED", errorMsg, executed: false);
			return StatusCode(StatusCodes.Status500InternalServerError, new { error = errorMsg });
		}

		var closed = closeResult.Result;
		var decision = closed ? "SIGNAL POSITION CLOSED" : "NO POSITION FOUND";
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
			_logger.LogError(ex, "Failed to send signal Discord notification for {Symbol}", alert.Symbol);
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
				Exchange = "signal-only",
				Direction = alert.Direction ?? "unknown",
				Price = alert.Price,
				Interval = alert.Interval ?? "unknown",
				StopLoss = null,
				TakeProfit = null,
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
