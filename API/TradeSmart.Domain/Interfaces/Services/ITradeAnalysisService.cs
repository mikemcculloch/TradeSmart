using TradeSmart.Domain.Entities;

namespace TradeSmart.Domain.Interfaces.Services;

/// <summary>Orchestrates trade analysis by fetching market data and invoking Claude.</summary>
public interface ITradeAnalysisService
{
	/// <summary>Analyzes a TradingView alert for trade opportunities.</summary>
	/// <param name="alert">The incoming TradingView alert.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A response containing the trade analysis.</returns>
	Task<ProxyResponse<TradeAnalysis>> AnalyzeAsync(
		TradingViewAlert alert,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Fire-and-forget audit: fetches market data, runs Claude analysis,
	/// and sends results to Discord. Does NOT execute trades.
	/// </summary>
	/// <param name="alert">The alert that was already executed.</param>
	/// <param name="executionResult">The result of the trade execution.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task AuditAsync(
		TradingViewAlert alert,
		TradeExecutionResult executionResult,
		CancellationToken cancellationToken = default);
}
