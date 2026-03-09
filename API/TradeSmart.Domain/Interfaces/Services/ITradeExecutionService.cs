using TradeSmart.Domain.Entities;

namespace TradeSmart.Domain.Interfaces.Services;

/// <summary>Executes trades from strategy signals or AI analysis.</summary>
public interface ITradeExecutionService
{
	/// <summary>Evaluates a completed analysis and opens a trade if criteria are met.</summary>
	/// <param name="analysis">The trade analysis from Claude.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The execution result indicating whether a trade was opened.</returns>
	Task<ProxyResponse<TradeExecutionResult>> ExecuteAsync(
		TradeAnalysis analysis,
		CancellationToken cancellationToken = default);

	/// <summary>Executes a trade directly from a TradingView strategy signal (no AI gating).</summary>
	/// <param name="alert">The incoming alert with direction, SL/TP from the strategy.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The execution result.</returns>
	Task<ProxyResponse<TradeExecutionResult>> ExecuteFromSignalAsync(
		TradingViewAlert alert,
		CancellationToken cancellationToken = default);

	/// <summary>Closes an open position for the given symbol (triggered by a close signal).</summary>
	/// <param name="alert">The close alert from TradingView.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Success or failure.</returns>
	Task<ProxyResponse<bool>> CloseFromSignalAsync(
		TradingViewAlert alert,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Executes a trade from a signal-only strategy (no SL/TP, 100% position sizing).
	/// Uses SignalTrading config section for position sizing and leverage.
	/// </summary>
	/// <param name="alert">The incoming alert with direction and price (no SL/TP expected).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The execution result.</returns>
	Task<ProxyResponse<TradeExecutionResult>> ExecuteSignalTradeAsync(
		TradingViewAlert alert,
		CancellationToken cancellationToken = default);
}
