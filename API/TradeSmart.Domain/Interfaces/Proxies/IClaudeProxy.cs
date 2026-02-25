using TradeSmart.Domain.Entities;

namespace TradeSmart.Domain.Interfaces.Proxies;

/// <summary>Proxy for sending trade analysis requests to Claude API.</summary>
public interface IClaudeProxy
{
	/// <summary>Sends market data and alert context to Claude for trade analysis.</summary>
	/// <param name="alert">The TradingView alert that triggered the analysis.</param>
	/// <param name="marketData">Market data across multiple timeframes.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A response containing the trade analysis result.</returns>
	Task<ProxyResponse<TradeAnalysis>> AnalyzeTradeAsync(
		TradingViewAlert alert,
		IReadOnlyList<TimeframeData> marketData,
		CancellationToken cancellationToken = default);
}
