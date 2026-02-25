using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;
using TradeSmart.Domain.Interfaces.Services;

namespace TradeSmart.Domain.Services;

/// <summary>Evaluates trade analysis results and decides whether to open paper positions.</summary>
public sealed class TradeExecutionService : ITradeExecutionService
{
	private readonly IConfiguration _configuration;
	private readonly IDiscordProxy _discordProxy;
	private readonly ILogger<TradeExecutionService> _logger;
	private readonly IPaperTradingService _paperTradingService;

	public TradeExecutionService(
		IPaperTradingService paperTradingService,
		IDiscordProxy discordProxy,
		IConfiguration configuration,
		ILogger<TradeExecutionService> logger)
	{
		_paperTradingService = paperTradingService;
		_discordProxy = discordProxy;
		_configuration = configuration;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<TradeExecutionResult>> ExecuteAsync(
		TradeAnalysis analysis,
		CancellationToken cancellationToken = default)
	{
		if (!_configuration.GetPaperTradingEnabled())
		{
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = "Paper trading is disabled",
				Analysis = analysis
			});
		}

		// 1. Check symbol is in allowed list
		var baseSymbol = ExtractBaseSymbol(analysis.Symbol);
		if (!Constants.PaperTrading.ALLOWED_SYMBOLS.Contains(baseSymbol, StringComparer.OrdinalIgnoreCase))
		{
			_logger.LogInformation(
				"Trade rejected for {Symbol}: not in allowed trading list",
				analysis.Symbol);

			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = $"Symbol {analysis.Symbol} (base: {baseSymbol}) is not in the allowed trading list",
				Analysis = analysis
			});
		}

		// 2. Check direction
		if (analysis.Direction == TradeDirection.NoTrade)
		{
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = "Analysis recommends no trade",
				Analysis = analysis
			});
		}

		// 3. Check confidence threshold
		var threshold = _configuration.GetPaperTradingConfidenceThreshold();
		if (analysis.Confidence < threshold)
		{
			_logger.LogInformation(
				"Trade rejected for {Symbol}: confidence {Confidence}% below threshold {Threshold}%",
				analysis.Symbol,
				analysis.Confidence,
				threshold);

			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = $"Confidence {analysis.Confidence}% below threshold {threshold}%",
				Analysis = analysis
			});
		}

		// 4. Check required price levels
		if (!analysis.EntryPrice.HasValue || !analysis.StopLoss.HasValue || !analysis.TakeProfit.HasValue)
		{
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = "Missing required price levels (entry, stop-loss, or take-profit)",
				Analysis = analysis
			});
		}

		// 5. Check position limits
		if (!_paperTradingService.CanOpenPosition())
		{
			_logger.LogInformation(
				"Trade rejected for {Symbol}: maximum concurrent positions reached or insufficient balance",
				analysis.Symbol);

			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = "Maximum concurrent positions reached or insufficient balance",
				Analysis = analysis
			});
		}

		// 6. Check duplicate symbol
		if (_paperTradingService.HasOpenPositionForSymbol(analysis.Symbol))
		{
			_logger.LogInformation(
				"Trade rejected for {Symbol}: already have an open position",
				analysis.Symbol);

			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = $"Already have an open position for {analysis.Symbol}",
				Analysis = analysis
			});
		}

		// 7. All checks pass — open the paper trade
		var openResult = await _paperTradingService.OpenPositionAsync(analysis, cancellationToken)
			.ConfigureAwait(false);

		if (openResult.HasErrors)
		{
			return ProxyResponse<TradeExecutionResult>.CreateError(
				openResult.Error!.Code,
				openResult.Error.Message);
		}

		// Fire-and-forget Discord notification
		_ = SendTradeOpenedNotificationAsync(openResult.Result!, cancellationToken);

		return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
		{
			TradeOpened = true,
			Position = openResult.Result,
			Analysis = analysis
		});
	}

	private static string ExtractBaseSymbol(string symbol)
	{
		// "BTC/USD" → "BTC", "XAU/USD" → "XAU"
		return symbol.Contains('/')
			? symbol.Split('/')[0]
			: symbol;
	}

	private async Task SendTradeOpenedNotificationAsync(
		PaperPosition position,
		CancellationToken cancellationToken)
	{
		try
		{
			var wallet = _paperTradingService.GetWallet();
			var result = await _discordProxy.SendTradeOpenedNotificationAsync(
				position, wallet, cancellationToken).ConfigureAwait(false);

			if (result.HasErrors)
			{
				_logger.LogWarning(
					"Discord trade-opened notification failed for {Symbol}: {ErrorMessage}",
					position.Symbol,
					result.Error!.Message);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unhandled error sending trade-opened Discord notification for {Symbol}", position.Symbol);
		}
	}
}
