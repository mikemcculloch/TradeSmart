using TradeSmart.Domain.Entities;

namespace TradeSmart.Domain.Interfaces.Proxies;

/// <summary>Persists signal logs and trade history to storage.</summary>
public interface ITradeHistoryProxy
{
	/// <summary>Records an incoming signal and its execution decision.</summary>
	Task<ProxyResponse<bool>> LogSignalAsync(
		SignalLogEntry entry,
		CancellationToken cancellationToken = default);

	/// <summary>Gets the most recent signal log entries.</summary>
	Task<ProxyResponse<IReadOnlyList<SignalLogEntry>>> GetRecentSignalsAsync(
		int count = 50,
		CancellationToken cancellationToken = default);

	/// <summary>Gets signal log entries for a specific symbol.</summary>
	Task<ProxyResponse<IReadOnlyList<SignalLogEntry>>> GetSignalsBySymbolAsync(
		string symbol,
		int count = 50,
		CancellationToken cancellationToken = default);
}
