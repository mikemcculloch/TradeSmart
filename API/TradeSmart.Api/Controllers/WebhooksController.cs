using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TradeSmart.Api.Dto;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Services;

namespace TradeSmart.Api.Controllers;

/// <summary>
/// Webhook endpoint for receiving TradingView strategy signals.
/// Entry signals execute trades immediately; Claude audits in the background for Discord insight.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class WebhooksController : ControllerBase
{
	private readonly IConfiguration _configuration;
	private readonly ILogger<WebhooksController> _logger;
	private readonly IMapper _mapper;
	private readonly ITradeAnalysisService _tradeAnalysisService;
	private readonly ITradeExecutionService _tradeExecutionService;

	public WebhooksController(
		ITradeExecutionService tradeExecutionService,
		ITradeAnalysisService tradeAnalysisService,
		IMapper mapper,
		ILogger<WebhooksController> logger,
		IConfiguration configuration)
	{
		_tradeExecutionService = tradeExecutionService;
		_tradeAnalysisService = tradeAnalysisService;
		_mapper = mapper;
		_logger = logger;
		_configuration = configuration;
	}

	/// <summary>
	/// Receives a TradingView strategy signal and executes immediately.
	/// Entry signals open positions; close signals close them.
	/// Claude analysis runs in the background for Discord audit logging.
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
			return Unauthorized(new { error = "Invalid webhook secret." });
		}

		var alert = _mapper.Map<TradingViewAlert>(request);

		_logger.LogInformation(
			"Received TradingView signal: {Type} {Symbol} {Direction} @ {Price}",
			alert.Type, alert.Symbol, alert.Direction ?? "n/a", alert.Price);

		// ── Close signals ───────────────────────────────────────────
		if (alert.IsClose)
		{
			var closeResult = await _tradeExecutionService.CloseFromSignalAsync(alert, cancellationToken);

			if (closeResult.HasErrors)
			{
				_logger.LogError("Close failed for {Symbol}: {Error}", alert.Symbol, closeResult.Error!.Message);
				return StatusCode(StatusCodes.Status500InternalServerError, new { error = closeResult.Error.Message });
			}

			return Ok(new { closed = closeResult.Result, symbol = alert.Symbol, message = alert.Message });
		}

		// ── Entry signals ───────────────────────────────────────────
		var executionResult = await _tradeExecutionService.ExecuteFromSignalAsync(alert, cancellationToken);

		if (executionResult.HasErrors)
		{
			_logger.LogError(
				"Execution failed for {Symbol}: [{Code}] {Error}",
				alert.Symbol, executionResult.Error!.Code, executionResult.Error.Message);
			return StatusCode(StatusCodes.Status500InternalServerError, new { error = executionResult.Error.Message });
		}

		// Fire-and-forget: Claude audit for Discord insight (no trade gating)
		_ = Task.Run(() => _tradeAnalysisService.AuditAsync(alert, executionResult.Result!, CancellationToken.None));

		var result = executionResult.Result!;
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
}
