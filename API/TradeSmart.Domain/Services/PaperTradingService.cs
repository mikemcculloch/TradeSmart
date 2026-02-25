using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;
using TradeSmart.Domain.Interfaces.Services;

namespace TradeSmart.Domain.Services;

/// <summary>
/// Manages paper trading state in memory with file-based persistence.
/// Registered as Singleton. All mutations are thread-safe via SemaphoreSlim.
/// </summary>
public sealed class PaperTradingService : IPaperTradingService, IDisposable
{
	private readonly IConfiguration _configuration;
	private readonly ILogger<PaperTradingService> _logger;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly SemaphoreSlim _lock = new(1, 1);

	private PaperWallet _wallet = new();
	private List<PaperPosition> _openPositions = [];
	private List<PaperPosition> _closedPositions = [];
	private bool _initialized;

	public PaperTradingService(
		IServiceScopeFactory scopeFactory,
		IConfiguration configuration,
		ILogger<PaperTradingService> logger)
	{
		_scopeFactory = scopeFactory;
		_configuration = configuration;
		_logger = logger;
	}

	/// <inheritdoc />
	public PaperWallet GetWallet() => _wallet;

	/// <inheritdoc />
	public IReadOnlyList<PaperPosition> GetOpenPositions() => _openPositions.ToList();

	/// <inheritdoc />
	public IReadOnlyList<PaperPosition> GetClosedPositions() => _closedPositions.ToList();

	/// <inheritdoc />
	public PaperTradingState GetState()
	{
		return new PaperTradingState
		{
			Wallet = _wallet,
			OpenPositions = _openPositions.ToList(),
			ClosedPositions = _closedPositions.ToList(),
			LastUpdatedAt = DateTimeOffset.UtcNow
		};
	}

	/// <inheritdoc />
	public bool CanOpenPosition()
	{
		var maxPositions = _configuration.GetPaperTradingMaxConcurrentPositions();
		return _openPositions.Count < maxPositions && _wallet.AvailableBalance > 0;
	}

	/// <inheritdoc />
	public bool HasOpenPositionForSymbol(string symbol)
	{
		return _openPositions.Any(p => p.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<PaperPosition>> OpenPositionAsync(
		TradeAnalysis analysis,
		CancellationToken cancellationToken = default)
	{
		await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

			// Validate direction
			if (analysis.Direction == TradeDirection.NoTrade)
			{
				return ProxyResponse<PaperPosition>.CreateError(
					Constants.ErrorCodes.INVALID_TRADE_PARAMETERS,
					"Cannot open a position with NoTrade direction.");
			}

			// Validate required price levels
			if (!analysis.EntryPrice.HasValue || !analysis.StopLoss.HasValue || !analysis.TakeProfit.HasValue)
			{
				return ProxyResponse<PaperPosition>.CreateError(
					Constants.ErrorCodes.INVALID_TRADE_PARAMETERS,
					"Missing required price levels (entry, stop-loss, or take-profit).");
			}

			// Check concurrent position limit
			var maxPositions = _configuration.GetPaperTradingMaxConcurrentPositions();
			if (_openPositions.Count >= maxPositions)
			{
				return ProxyResponse<PaperPosition>.CreateError(
					Constants.ErrorCodes.POSITION_LIMIT_REACHED,
					$"Maximum concurrent positions ({maxPositions}) reached.");
			}

			// Check duplicate symbol
			if (_openPositions.Any(p => p.Symbol.Equals(analysis.Symbol, StringComparison.OrdinalIgnoreCase)))
			{
				return ProxyResponse<PaperPosition>.CreateError(
					Constants.ErrorCodes.DUPLICATE_SYMBOL_POSITION,
					$"Already have an open position for {analysis.Symbol}.");
			}

			// Calculate position size
			var maxSizePercent = _configuration.GetPaperTradingMaxPositionSizePercent();
			var positionSizeUsd = _wallet.AvailableBalance * maxSizePercent;

			if (positionSizeUsd <= 0)
			{
				return ProxyResponse<PaperPosition>.CreateError(
					Constants.ErrorCodes.INSUFFICIENT_BALANCE,
					"Insufficient wallet balance to open a new position.");
			}

			var leverage = _configuration.GetPaperTradingLeverage();
			var entryPrice = analysis.EntryPrice.Value;

			// Enforce stop-loss cap
			var effectiveStopLoss = CalculateEffectiveStopLoss(
				analysis.Direction, entryPrice, analysis.StopLoss.Value);

			// Calculate quantity (notional / entry price)
			var quantity = positionSizeUsd * leverage / entryPrice;

			var position = new PaperPosition
			{
				Symbol = analysis.Symbol,
				Direction = analysis.Direction,
				EntryPrice = entryPrice,
				PositionSizeUsd = positionSizeUsd,
				Quantity = quantity,
				Leverage = leverage,
				StopLoss = effectiveStopLoss,
				TakeProfit = analysis.TakeProfit.Value,
				Confidence = analysis.Confidence,
				Reasoning = analysis.Reasoning
			};

			_openPositions.Add(position);

			_wallet = _wallet with
			{
				AvailableBalance = _wallet.AvailableBalance - positionSizeUsd,
				TotalTrades = _wallet.TotalTrades + 1
			};

			await PersistStateAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogInformation(
				"Paper trade opened: {Symbol} {Direction} @ {EntryPrice}, Size=${PositionSize}, SL={StopLoss}, TP={TakeProfit}",
				position.Symbol,
				position.Direction,
				position.EntryPrice,
				position.PositionSizeUsd,
				position.StopLoss,
				position.TakeProfit);

			return ProxyResponse<PaperPosition>.Success(position);
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<PositionCloseResult>> ClosePositionAsync(
		string positionId,
		decimal exitPrice,
		string closeReason,
		CancellationToken cancellationToken = default)
	{
		await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

			var position = _openPositions.FirstOrDefault(
				p => p.PositionId.Equals(positionId, StringComparison.OrdinalIgnoreCase));

			if (position is null)
			{
				return ProxyResponse<PositionCloseResult>.CreateError(
					Constants.ErrorCodes.POSITION_NOT_FOUND,
					$"Position {positionId} not found in open positions.");
			}

			// Calculate PnL
			var pnl = CalculatePnl(position, exitPrice);

			var closedPosition = position with
			{
				ExitPrice = exitPrice,
				RealizedPnl = pnl,
				ClosedAt = DateTimeOffset.UtcNow,
				CloseReason = closeReason
			};

			_openPositions.Remove(position);
			_closedPositions.Add(closedPosition);

			// Credit wallet: return collateral + PnL
			var newBalance = _wallet.AvailableBalance + position.PositionSizeUsd + pnl;
			if (newBalance < 0) newBalance = 0; // cannot go negative

			_wallet = _wallet with
			{
				AvailableBalance = newBalance,
				TotalRealizedPnl = _wallet.TotalRealizedPnl + pnl,
				WinningTrades = pnl >= 0 ? _wallet.WinningTrades + 1 : _wallet.WinningTrades,
				LosingTrades = pnl < 0 ? _wallet.LosingTrades + 1 : _wallet.LosingTrades
			};

			await PersistStateAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogInformation(
				"Paper trade closed: {Symbol} {Direction} @ {ExitPrice}, PnL={Pnl:+#.##;-#.##;0} USD ({CloseReason})",
				closedPosition.Symbol,
				closedPosition.Direction,
				exitPrice,
				pnl,
				closeReason);

			return ProxyResponse<PositionCloseResult>.Success(new PositionCloseResult
			{
				ClosedPosition = closedPosition,
				UpdatedWallet = _wallet
			});
		}
		finally
		{
			_lock.Release();
		}
	}

	public void Dispose()
	{
		_lock.Dispose();
	}

	private decimal CalculateEffectiveStopLoss(TradeDirection direction, decimal entryPrice, decimal claudeStopLoss)
	{
		var maxSlPercent = _configuration.GetPaperTradingMaxStopLossPercent();
		var slDistance = Math.Abs(claudeStopLoss - entryPrice) / entryPrice;

		if (slDistance <= maxSlPercent)
		{
			return claudeStopLoss;
		}

		// Cap at max allowed percentage
		var capped = direction == TradeDirection.Long
			? entryPrice * (1 - maxSlPercent)
			: entryPrice * (1 + maxSlPercent);

		_logger.LogInformation(
			"Capped stop-loss from {OriginalSL} to {CappedSL} (max {MaxPercent}%)",
			claudeStopLoss,
			capped,
			maxSlPercent * 100);

		return capped;
	}

	private static decimal CalculatePnl(PaperPosition position, decimal exitPrice)
	{
		// PnL = (priceChange / entryPrice) * positionSize * leverage
		var priceChange = position.Direction == TradeDirection.Long
			? exitPrice - position.EntryPrice
			: position.EntryPrice - exitPrice;

		return priceChange / position.EntryPrice * position.PositionSizeUsd * position.Leverage;
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized) return;

		using var scope = _scopeFactory.CreateScope();
		var stateProxy = scope.ServiceProvider.GetRequiredService<IPaperTradingStateProxy>();
		var result = await stateProxy.LoadStateAsync(cancellationToken).ConfigureAwait(false);

		if (!result.HasErrors && result.Result is not null)
		{
			_wallet = result.Result.Wallet;
			_openPositions = result.Result.OpenPositions.ToList();
			_closedPositions = result.Result.ClosedPositions.ToList();
		}
		else
		{
			var initialBalance = _configuration.GetPaperTradingInitialBalance();
			_wallet = new PaperWallet
			{
				InitialBalance = initialBalance,
				AvailableBalance = initialBalance
			};
			_openPositions = [];
			_closedPositions = [];
		}

		_initialized = true;
	}

	private async Task PersistStateAsync(CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();
		var stateProxy = scope.ServiceProvider.GetRequiredService<IPaperTradingStateProxy>();

		var state = new PaperTradingState
		{
			Wallet = _wallet,
			OpenPositions = _openPositions.ToList(),
			ClosedPositions = _closedPositions.ToList(),
			LastUpdatedAt = DateTimeOffset.UtcNow
		};

		var result = await stateProxy.SaveStateAsync(state, cancellationToken).ConfigureAwait(false);
		if (result.HasErrors)
		{
			_logger.LogError("Failed to persist paper trading state: {ErrorMessage}", result.Error!.Message);
		}
	}
}
