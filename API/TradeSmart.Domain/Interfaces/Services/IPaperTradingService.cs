using TradeSmart.Domain.Entities;

namespace TradeSmart.Domain.Interfaces.Services;

/// <summary>Manages paper trading wallet state and positions. Thread-safe singleton.</summary>
public interface IPaperTradingService
{
	/// <summary>Gets the current wallet state.</summary>
	PaperWallet GetWallet();

	/// <summary>Gets all currently open positions.</summary>
	IReadOnlyList<PaperPosition> GetOpenPositions();

	/// <summary>Gets closed position history.</summary>
	IReadOnlyList<PaperPosition> GetClosedPositions();

	/// <summary>Gets the full state snapshot.</summary>
	PaperTradingState GetState();

	/// <summary>Opens a new paper position.</summary>
	/// <param name="analysis">The trade analysis driving the position.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The opened position, or an error if constraints are violated.</returns>
	Task<ProxyResponse<PaperPosition>> OpenPositionAsync(
		TradeAnalysis analysis,
		CancellationToken cancellationToken = default);

	/// <summary>Closes a position at the specified price.</summary>
	/// <param name="positionId">The position ID to close.</param>
	/// <param name="exitPrice">The exit price.</param>
	/// <param name="closeReason">The reason for closing.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The close result with PnL, or an error.</returns>
	Task<ProxyResponse<PositionCloseResult>> ClosePositionAsync(
		string positionId,
		decimal exitPrice,
		string closeReason,
		CancellationToken cancellationToken = default);

	/// <summary>Checks if a new position can be opened (concurrent position limit, balance).</summary>
	bool CanOpenPosition();

	/// <summary>Checks if a symbol already has an open position.</summary>
	bool HasOpenPositionForSymbol(string symbol);
}
