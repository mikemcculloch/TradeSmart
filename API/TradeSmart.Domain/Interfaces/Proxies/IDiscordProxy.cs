using TradeSmart.Domain.Entities;

namespace TradeSmart.Domain.Interfaces.Proxies;

/// <summary>Proxy for sending trade notifications to Discord via webhook.</summary>
public interface IDiscordProxy
{
	/// <summary>Sends a trade analysis notification to the configured Discord channel.</summary>
	/// <param name="alert">The TradingView alert that triggered the analysis.</param>
	/// <param name="analysis">The completed trade analysis from Claude.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A response indicating success or failure.</returns>
	Task<ProxyResponse<bool>> SendTradeNotificationAsync(
		TradingViewAlert alert,
		TradeAnalysis analysis,
		CancellationToken cancellationToken = default);
}
