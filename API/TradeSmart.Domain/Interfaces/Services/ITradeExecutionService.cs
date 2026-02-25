using TradeSmart.Domain.Entities;

namespace TradeSmart.Domain.Interfaces.Services;

/// <summary>Evaluates trade analyses and decides whether to open paper positions.</summary>
public interface ITradeExecutionService
{
	/// <summary>Evaluates a completed analysis and opens a paper trade if criteria are met.</summary>
	/// <param name="analysis">The trade analysis from Claude.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The execution result indicating whether a trade was opened.</returns>
	Task<ProxyResponse<TradeExecutionResult>> ExecuteAsync(
		TradeAnalysis analysis,
		CancellationToken cancellationToken = default);
}
