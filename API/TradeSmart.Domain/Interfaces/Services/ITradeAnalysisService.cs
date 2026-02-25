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
}
