using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;
using TradeSmart.Domain.Interfaces.Services;

namespace TradeSmart.Domain.Services;

/// <summary>
/// Executes trades from strategy signals or AI analysis.
/// Supports Paper (in-memory wallet) and Live (Bitunix exchange) modes.
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

	// ════════════════════════════════════════════════════════════════════
	//  ExecuteFromSignalAsync — PRIMARY: direct execution from strategy
	// ════════════════════════════════════════════════════════════════════

	/// <inheritdoc />
	public async Task<ProxyResponse<TradeExecutionResult>> ExecuteFromSignalAsync(
		TradingViewAlert alert,
		CancellationToken cancellationToken = default)
	{
		var direction = alert.ParsedDirection;

		if (direction == TradeDirection.NoTrade)
		{
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = $"Could not determine direction from signal (direction={alert.Direction})",
				Analysis = BuildAnalysisFromSignal(alert, direction)
			});
		}

		// Normalize symbol for internal use
		var normalizedSymbol = SymbolNormalizer.Normalize(alert.Symbol);

		// Check symbol is in allowed list
		var baseSymbol = ExtractBaseSymbol(normalizedSymbol);
		if (!Constants.PaperTrading.ALLOWED_SYMBOLS.Contains(baseSymbol, StringComparer.OrdinalIgnoreCase))
		{
			_logger.LogInformation("Trade rejected for {Symbol}: not in allowed trading list", alert.Symbol);
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = $"Symbol {alert.Symbol} (base: {baseSymbol}) is not in the allowed trading list",
				Analysis = BuildAnalysisFromSignal(alert, direction)
			});
		}

		// Build a TradeAnalysis from the strategy signal (100% confidence — strategy already decided)
		var analysis = BuildAnalysisFromSignal(alert, direction) with
		{
			Symbol = normalizedSymbol
		};

		// Check required price levels
		if (!analysis.EntryPrice.HasValue || !analysis.StopLoss.HasValue || !analysis.TakeProfit.HasValue)
		{
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = "Missing required price levels (entry, stop-loss, or take-profit) from strategy signal",
				Analysis = analysis
			});
		}

		_logger.LogInformation(
			"Executing from strategy signal: {Symbol} {Direction} @ {Price}, SL={StopLoss}, TP={TakeProfit}",
			normalizedSymbol,
			direction,
			analysis.EntryPrice,
			analysis.StopLoss,
			analysis.TakeProfit);

		// Route to the appropriate execution mode
		var tradingMode = _configuration.GetTradingMode();
		return tradingMode switch
		{
			TradingMode.Live => await ExecuteLiveTradeAsync(analysis, cancellationToken).ConfigureAwait(false),
			_ => await ExecutePaperTradeAsync(analysis, cancellationToken).ConfigureAwait(false)
		};
	}

	// ════════════════════════════════════════════════════════════════════
	//  CloseFromSignalAsync — closes position from strategy exit signal
	// ════════════════════════════════════════════════════════════════════

	/// <inheritdoc />
	public async Task<ProxyResponse<bool>> CloseFromSignalAsync(
		TradingViewAlert alert,
		CancellationToken cancellationToken = default)
	{
		var normalizedSymbol = SymbolNormalizer.Normalize(alert.Symbol);
		var tradingMode = _configuration.GetTradingMode();

		_logger.LogInformation(
			"Close signal received for {Symbol} in {Mode} mode: {Message}",
			normalizedSymbol,
			tradingMode,
			alert.Message);

		if (tradingMode == TradingMode.Live)
		{
			return await CloseLivePositionAsync(normalizedSymbol, alert, cancellationToken).ConfigureAwait(false);
		}

		return await ClosePaperPositionAsync(normalizedSymbol, alert, cancellationToken).ConfigureAwait(false);
	}

	// ════════════════════════════════════════════════════════════════════
	//  ExecuteAsync — LEGACY: AI-analysis gated execution (kept for audit)
	// ════════════════════════════════════════════════════════════════════

	/// <inheritdoc />
	public async Task<ProxyResponse<TradeExecutionResult>> ExecuteAsync(
		TradeAnalysis analysis,
		CancellationToken cancellationToken = default)
	{
		var tradingMode = _configuration.GetTradingMode();

		// Check symbol is in allowed list
		var baseSymbol = ExtractBaseSymbol(analysis.Symbol);
		if (!Constants.PaperTrading.ALLOWED_SYMBOLS.Contains(baseSymbol, StringComparer.OrdinalIgnoreCase))
		{
			_logger.LogInformation("Trade rejected for {Symbol}: not in allowed trading list", analysis.Symbol);
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = $"Symbol {analysis.Symbol} (base: {baseSymbol}) is not in the allowed trading list",
				Analysis = analysis
			});
		}

		if (analysis.Direction == TradeDirection.NoTrade)
		{
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = "Analysis recommends no trade",
				Analysis = analysis
			});
		}

		var threshold = _configuration.GetPaperTradingConfidenceThreshold();
		if (analysis.Confidence < threshold)
		{
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = $"Confidence {analysis.Confidence}% below threshold {threshold}%",
				Analysis = analysis
			});
		}

		if (!analysis.EntryPrice.HasValue || !analysis.StopLoss.HasValue || !analysis.TakeProfit.HasValue)
		{
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = "Missing required price levels (entry, stop-loss, or take-profit)",
				Analysis = analysis
			});
		}

		return tradingMode switch
		{
			TradingMode.Live => await ExecuteLiveTradeAsync(analysis, cancellationToken).ConfigureAwait(false),
			_ => await ExecutePaperTradeAsync(analysis, cancellationToken).ConfigureAwait(false)
		};
	}

	// ── Paper Trading ───────────────────────────────────────────────────

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

		if (!_paperTradingService.CanOpenPosition())
		{
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = "Maximum concurrent positions reached or insufficient balance",
				Analysis = analysis
			});
		}

		if (_paperTradingService.HasOpenPositionForSymbol(analysis.Symbol))
		{
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = $"Already have an open position for {analysis.Symbol}",
				Analysis = analysis
			});
		}

		var openResult = await _paperTradingService.OpenPositionAsync(analysis, cancellationToken)
			.ConfigureAwait(false);

		if (openResult.HasErrors)
		{
			return ProxyResponse<TradeExecutionResult>.CreateError(
				openResult.Error!.Code,
				openResult.Error.Message);
		}

		_ = SendTradeOpenedNotificationAsync(openResult.Result!, cancellationToken);

		return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
		{
			TradeOpened = true,
			Position = openResult.Result,
			Analysis = analysis
		});
	}

	private async Task<ProxyResponse<bool>> ClosePaperPositionAsync(
		string normalizedSymbol,
		TradingViewAlert alert,
		CancellationToken cancellationToken)
	{
		var openPositions = _paperTradingService.GetOpenPositions();
		var position = openPositions.FirstOrDefault(
			p => p.Symbol.Equals(normalizedSymbol, StringComparison.OrdinalIgnoreCase));

		if (position is null)
		{
			_logger.LogInformation("No open paper position found for {Symbol} to close", normalizedSymbol);
			return ProxyResponse<bool>.Success(false);
		}

		var closeResult = await _paperTradingService.ClosePositionAsync(
			position.PositionId, alert.Price, CloseReason.StrategyExit, cancellationToken)
			.ConfigureAwait(false);

		if (closeResult.HasErrors)
		{
			_logger.LogError(
				"Failed to close paper position for {Symbol}: {Error}",
				normalizedSymbol,
				closeResult.Error!.Message);
			return ProxyResponse<bool>.CreateError(closeResult.Error.Code, closeResult.Error.Message);
		}

		_logger.LogInformation(
			"Paper position closed for {Symbol}: PnL={Pnl:+#.##;-#.##;0} USD ({Reason})",
			normalizedSymbol,
			closeResult.Result!.ClosedPosition.RealizedPnl,
			alert.Message);

		_ = SendTradeClosedNotificationAsync(closeResult.Result.ClosedPosition, closeResult.Result.UpdatedWallet, cancellationToken);
		return ProxyResponse<bool>.Success(true);
	}

	// ── Live Trading (Bitunix) ──────────────────────────────────────────

	private async Task<ProxyResponse<TradeExecutionResult>> ExecuteLiveTradeAsync(
		TradeAnalysis analysis,
		CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"Executing LIVE trade for {Symbol}: {Direction} @ {EntryPrice}",
			analysis.Symbol,
			analysis.Direction,
			analysis.EntryPrice);

		var bitunixSymbol = ToBitunixSymbol(analysis.Symbol);
		var maxSizePercent = _configuration.GetPaperTradingMaxPositionSizePercent();
		var leverage = _configuration.GetPaperTradingLeverage();

		var accountResult = await _bitunixProxy.GetAccountAsync(cancellationToken).ConfigureAwait(false);
		if (accountResult.HasErrors)
		{
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = $"Failed to get exchange account info: {accountResult.Error!.Message}",
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
		var quantity = Math.Round(positionSizeUsd * leverage / entryPrice, 6);

		var orderRequest = new BitunixOrderRequest
		{
			Symbol = bitunixSymbol,
			Qty = quantity,
			Side = analysis.Direction == TradeDirection.Long ? "BUY" : "SELL",
			TradeSide = "OPEN",
			OrderType = "MARKET",
			TpPrice = analysis.TakeProfit,
			SlPrice = analysis.StopLoss
		};

		var orderResult = await _bitunixProxy.PlaceOrderAsync(orderRequest, cancellationToken)
			.ConfigureAwait(false);

		if (orderResult.HasErrors)
		{
			return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
			{
				TradeOpened = false,
				RejectionReason = $"Exchange order rejected: {orderResult.Error!.Message}",
				Analysis = analysis
			});
		}

		_logger.LogInformation(
			"LIVE trade opened: {Symbol} {Direction} {Qty} @ MARKET, OrderId={OrderId}",
			analysis.Symbol, analysis.Direction, quantity, orderResult.Result!.OrderId);

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

		_ = SendLiveTradeNotificationAsync(position, availableBalance, cancellationToken);

		return ProxyResponse<TradeExecutionResult>.Success(new TradeExecutionResult
		{
			TradeOpened = true,
			Position = position,
			Analysis = analysis
		});
	}

	private async Task<ProxyResponse<bool>> CloseLivePositionAsync(
		string normalizedSymbol,
		TradingViewAlert alert,
		CancellationToken cancellationToken)
	{
		var bitunixSymbol = ToBitunixSymbol(normalizedSymbol);

		// Get open positions from exchange to find the one to close
		var positionsResult = await _bitunixProxy.GetPositionsAsync(cancellationToken).ConfigureAwait(false);
		if (positionsResult.HasErrors)
		{
			_logger.LogError("Cannot close live position: failed to get positions — {Error}", positionsResult.Error!.Message);
			return ProxyResponse<bool>.CreateError(
				Constants.ErrorCodes.BITUNIX_API_ERROR,
				$"Failed to get exchange positions: {positionsResult.Error.Message}");
		}

		var position = positionsResult.Result!.FirstOrDefault(
			p => p.Symbol.Equals(bitunixSymbol, StringComparison.OrdinalIgnoreCase));

		if (position is null)
		{
			_logger.LogInformation("No open live position found for {Symbol} to close", bitunixSymbol);
			return ProxyResponse<bool>.Success(false);
		}

		// Close by placing an opposing MARKET order
		var closeSide = position.Side.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? "SELL" : "BUY";
		var closeOrder = new BitunixOrderRequest
		{
			Symbol = bitunixSymbol,
			Qty = position.Qty,
			Side = closeSide,
			TradeSide = "CLOSE",
			OrderType = "MARKET"
		};

		var closeResult = await _bitunixProxy.PlaceOrderAsync(closeOrder, cancellationToken).ConfigureAwait(false);
		if (closeResult.HasErrors)
		{
			_logger.LogError("Failed to close live position for {Symbol}: {Error}", bitunixSymbol, closeResult.Error!.Message);
			return ProxyResponse<bool>.CreateError(
				Constants.ErrorCodes.BITUNIX_API_ERROR,
				$"Failed to close position: {closeResult.Error.Message}");
		}

		_logger.LogInformation(
			"LIVE position closed for {Symbol}: OrderId={OrderId}, Reason={Reason}",
			bitunixSymbol, closeResult.Result!.OrderId, alert.Message);

		return ProxyResponse<bool>.Success(true);
	}

	// ── Helpers ──────────────────────────────────────────────────────────

	/// <summary>Builds a TradeAnalysis record from a strategy signal for consistent downstream handling.</summary>
	private static TradeAnalysis BuildAnalysisFromSignal(TradingViewAlert alert, TradeDirection direction)
	{
		return new TradeAnalysis
		{
			Symbol = SymbolNormalizer.Normalize(alert.Symbol),
			Direction = direction,
			Confidence = 100, // Strategy already made the decision
			EntryPrice = alert.Price,
			StopLoss = alert.StopLoss,
			TakeProfit = alert.TakeProfit,
			Reasoning = $"Strategy signal: {alert.Message}"
		};
	}

	private static string ExtractBaseSymbol(string symbol)
	{
		return symbol.Contains('/') ? symbol.Split('/')[0] : symbol;
	}

	private static string ToBitunixSymbol(string symbol)
	{
		if (symbol.Contains('/'))
		{
			var parts = symbol.Split('/');
			return $"{parts[0]}USDT";
		}

		if (symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
			return symbol.ToUpperInvariant();

		return $"{symbol.ToUpperInvariant()}USDT";
	}

	private async Task SendTradeOpenedNotificationAsync(PaperPosition position, CancellationToken cancellationToken)
	{
		try
		{
			var wallet = _paperTradingService.GetWallet();
			await _discordProxy.SendTradeOpenedNotificationAsync(position, wallet, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Discord trade-opened notification error for {Symbol}", position.Symbol);
		}
	}

	private async Task SendTradeClosedNotificationAsync(PaperPosition position, PaperWallet wallet, CancellationToken cancellationToken)
	{
		try
		{
			await _discordProxy.SendTradeClosedNotificationAsync(position, wallet, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Discord trade-closed notification error for {Symbol}", position.Symbol);
		}
	}

	private async Task SendLiveTradeNotificationAsync(PaperPosition position, decimal availableBalance, CancellationToken cancellationToken)
	{
		try
		{
			var wallet = new PaperWallet
			{
				InitialBalance = 1000m,
				AvailableBalance = availableBalance
			};
			await _discordProxy.SendTradeOpenedNotificationAsync(position, wallet, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Discord live-trade notification error for {Symbol}", position.Symbol);
		}
	}
}
