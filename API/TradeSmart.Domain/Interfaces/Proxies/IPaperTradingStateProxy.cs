using TradeSmart.Domain.Entities;

namespace TradeSmart.Domain.Interfaces.Proxies;

/// <summary>Proxy for persisting and loading paper trading state from storage.</summary>
public interface IPaperTradingStateProxy
{
	/// <summary>Loads the paper trading state from persistent storage.</summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The loaded state, or a default state if none exists.</returns>
	Task<ProxyResponse<PaperTradingState>> LoadStateAsync(
		CancellationToken cancellationToken = default);

	/// <summary>Saves the paper trading state to persistent storage.</summary>
	/// <param name="state">The state to persist.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Success or failure.</returns>
	Task<ProxyResponse<bool>> SaveStateAsync(
		PaperTradingState state,
		CancellationToken cancellationToken = default);
}
