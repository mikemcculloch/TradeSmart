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

	/// <summary>Sends a notification when a paper trade is opened.</summary>
	/// <param name="position">The opened position.</param>
	/// <param name="wallet">The current wallet state.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A response indicating success or failure.</returns>
	Task<ProxyResponse<bool>> SendTradeOpenedNotificationAsync(
		PaperPosition position,
		PaperWallet wallet,
		CancellationToken cancellationToken = default);

	/// <summary>Sends a notification when a paper trade is closed.</summary>
	/// <param name="closedPosition">The closed position with PnL.</param>
	/// <param name="wallet">The updated wallet state.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A response indicating success or failure.</returns>
	Task<ProxyResponse<bool>> SendTradeClosedNotificationAsync(
		PaperPosition closedPosition,
		PaperWallet wallet,
		CancellationToken cancellationToken = default);
}
