using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TradeSmart.Api.Dto;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Services;

namespace TradeSmart.Api.Controllers;

/// <summary>Webhook endpoint for receiving TradingView alerts and returning trade analysis.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class WebhooksController : ControllerBase
{
	private readonly IConfiguration _configuration;
	private readonly ILogger<WebhooksController> _logger;
	private readonly IMapper _mapper;
	private readonly ITradeAnalysisService _tradeAnalysisService;

	public WebhooksController(
		ITradeAnalysisService tradeAnalysisService,
		IMapper mapper,
		ILogger<WebhooksController> logger,
		IConfiguration configuration)
	{
		_tradeAnalysisService = tradeAnalysisService;
		_mapper = mapper;
		_logger = logger;
		_configuration = configuration;
	}

	/// <summary>Receives a TradingView alert and returns an AI-powered trade analysis.</summary>
	/// <param name="request">The TradingView alert payload.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Trade analysis with direction, confidence, entry/exit levels, and reasoning.</returns>
	[HttpPost("tradingview")]
	[ProducesResponseType(typeof(TradeAnalysisResponseDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public async Task<ActionResult<TradeAnalysisResponseDto>> ReceiveTradingViewAlertAsync(
		[FromBody] TradingViewAlertRequestDto request,
		CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
		{
			return ValidationProblem(ModelState);
		}

		// Validate webhook secret
		var expectedSecret = _configuration.GetWebhookSecret();
		if (!string.IsNullOrWhiteSpace(expectedSecret) && request.Secret != expectedSecret)
		{
			_logger.LogWarning("Webhook authentication failed for {Symbol}", request.Symbol);
			return Unauthorized(new { error = "Invalid webhook secret." });
		}

		_logger.LogInformation(
			"Received TradingView alert for {Symbol} on {Exchange} — action: {Action}",
			request.Symbol,
			request.Exchange,
			request.Action);

		var alert = _mapper.Map<TradingViewAlert>(request);

		var result = await _tradeAnalysisService.AnalyzeAsync(alert, cancellationToken);

		if (result.HasErrors)
		{
			_logger.LogError(
				"Trade analysis failed for {Symbol}: [{ErrorCode}] {ErrorMessage}",
				request.Symbol,
				result.Error!.Code,
				result.Error.Message);

			return StatusCode(
				StatusCodes.Status500InternalServerError,
				new { error = result.Error.Message });
		}

		var response = _mapper.Map<TradeAnalysisResponseDto>(result.Result);
		return Ok(response);
	}
}
