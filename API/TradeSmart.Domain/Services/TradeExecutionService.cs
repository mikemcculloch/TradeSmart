using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;
using TradeSmart.Domain.Interfaces.Services;

namespace TradeSmart.Domain.Services;

/// <summary>
/// Evaluates trade analysis results and decides whether to open positions.
/// Supports both Paper and Live (Bitunix) execution modes via configuration.
/// </summary>
public sealed class TradeExecutionService : ITradeExecutionService
{
	private readonly IBitunixProxy _bitunixProxy;
	private readonly IConfiguration _configuration;
	private readonly IDiscordProxy _discordProxy;
	private readonly ILogger<TradeExecutionService> _logger;
	private readonly IPaperTradingService _paperTradingService;

	public TradeExecutionService(
		IPaperTradingService paperTradingService,
		IBitunixProxy bitunixProxy,
		IDiscordProxy discordProxy,
		IConfiguration configuration,
		ILogger<TradeExecutionService> logger)
	{
		_paperTradingService = paperTradingService;
		_bitunixProxy = bitunixProxy;
		_discordProxy = discordProxy;
		_configuration = configuration;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<TradeExecutionResult>> ExecuteAsync(
		TradeAnalysis analysis,
		CancellationToken cancellationToken = default)
	{
		var tradingMode = _configuration.GetTradingMode();

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

		// 5. Route to the appropriate execution mode
		return tradingMode switch
		{
			TradingMode.Live => await ExecuteLiveTradeAsync(analysis, cancellationToken).ConfigureAwait(false),
			_ => await ExecutePaperTradeAsync(analysis, cancellationToken).ConfigureAwait(false)
		};
	}

	// ── Paper Trading Path ──────────────────────────────────────────────

	private async Task<ProxyResponse<TradeExecutionResult>> ExecutePaperTradeAsync(
		TradeAnalysis analysis,
		CancellationToken cancellationToken)
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

		// Check position limits
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

		// Check duplicate symbol
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

		// Open the paper trade
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

	// ── Live Trading Path (Bitunix) ─────────────────────────────────────

	private async Task<ProxyResponse<TradeExecutionResult>> ExecuteLiveTradeAsync(
		TradeAnalysis analysis,
		CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"Executing LIVE trade for {Symbol}: {Direction} @ {EntryPrice}",
			analysis.Symbol,
			analysis.Direction,
			analysis.EntryPrice);

		// Convert TradeSmart symbol (e.g., "BTC/USD") to Bitunix format (e.g., "BTCUSDT")
		var bitunixSymbol = ToBitunixSymbol(analysis.Symbol);

		// Calculate quantity: use position sizing from config
		var maxSizePercent = _configuration.GetPaperTradingMaxPositionSizePercent();
		var leverage = _configuration.GetPaperTradingLeverage();

		// Get current account balance to calculate position size
		var accountResult = await _bitunixProxy.GetAccountAsync(cancellationToken).ConfigureAwait(false);
		if (accountResult.HasErrors)
		{
			_logger.LogError(
				"Cannot execute live trade for {Symbol}: failed to get account info — {Error}",
				analysis.Symbol,
				accountResult.Error!.Message);

			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = $"Failed to get exchange account info: {accountResult.Error.Message}",
				Analysis = analysis
			});
		}

		var availableBalance = accountResult.Result!.Available;
		var positionSizeUsd = availableBalance * maxSizePercent;

		if (positionSizeUsd <= 0)
		{
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = "Insufficient exchange balance to open a new position",
				Analysis = analysis
			});
		}

		var entryPrice = analysis.EntryPrice!.Value;
		var quantity = positionSizeUsd * leverage / entryPrice;

		// Round quantity to reasonable precision
		quantity = Math.Round(quantity, 6);

		var side = analysis.Direction == TradeDirection.Long ? "BUY" : "SELL";

		var orderRequest = new BitunixOrderRequest
		{
			Symbol = bitunixSymbol,
			Qty = quantity,
			Side = side,
			TradeSide = "OPEN",
			OrderType = "MARKET",
			TpPrice = analysis.TakeProfit,
			SlPrice = analysis.StopLoss
		};

		var orderResult = await _bitunixProxy.PlaceOrderAsync(orderRequest, cancellationToken)
			.ConfigureAwait(false);

		if (orderResult.HasErrors)
		{
			_logger.LogError(
				"Live order failed for {Symbol}: {Error}",
				analysis.Symbol,
				orderResult.Error!.Message);

			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = $"Exchange order rejected: {orderResult.Error.Message}",
				Analysis = analysis
			});
		}

		_logger.LogInformation(
			"LIVE trade opened for {Symbol}: {Direction} {Qty} @ MARKET, OrderId={OrderId}",
			analysis.Symbol,
			analysis.Direction,
			quantity,
			orderResult.Result!.OrderId);

		// Build a PaperPosition record for response consistency (reuse the same DTO shape)
		var position = new PaperPosition
		{
			PositionId = orderResult.Result.OrderId,
			Symbol = analysis.Symbol,
			Direction = analysis.Direction,
			EntryPrice = entryPrice,
			PositionSizeUsd = positionSizeUsd,
			Quantity = quantity,
			Leverage = leverage,
			StopLoss = analysis.StopLoss!.Value,
			TakeProfit = analysis.TakeProfit!.Value,
			Confidence = analysis.Confidence,
			Reasoning = analysis.Reasoning
		};

		// Send Discord notification
		_ = SendLiveTradeNotificationAsync(position, availableBalance, cancellationToken);

		return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
		{
			TradeOpened = true,
			Position = position,
			Analysis = analysis
		});
	}

	// ── Helpers ──────────────────────────────────────────────────────────

	private static string ExtractBaseSymbol(string symbol)
	{
		// "BTC/USD" → "BTC", "XAU/USD" → "XAU"
		return symbol.Contains('/')
			? symbol.Split('/')[0]
			: symbol;
	}

	/// <summary>Converts TradeSmart symbol (e.g. "BTC/USD") to Bitunix futures format ("BTCUSDT").</summary>
	private static string ToBitunixSymbol(string symbol)
	{
		if (symbol.Contains('/'))
		{
			var parts = symbol.Split('/');
			return $"{parts[0]}USDT";
		}

		// Already in exchange format or close to it
		if (symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
			return symbol.ToUpperInvariant();

		return $"{symbol.ToUpperInvariant()}USDT";
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

	private async Task SendLiveTradeNotificationAsync(
		PaperPosition position,
		decimal availableBalance,
		CancellationToken cancellationToken)
	{
		try
		{
			// Reuse the same Discord method; wallet shows exchange balance
			var wallet = new PaperWallet
			{
				InitialBalance = 1000m, // placeholder
				AvailableBalance = availableBalance
			};

			var result = await _discordProxy.SendTradeOpenedNotificationAsync(
				position, wallet, cancellationToken).ConfigureAwait(false);

			if (result.HasErrors)
			{
				_logger.LogWarning(
					"Discord live-trade notification failed for {Symbol}: {ErrorMessage}",
					position.Symbol,
					result.Error!.Message);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unhandled error sending live-trade Discord notification for {Symbol}", position.Symbol);
		}
	}
}
