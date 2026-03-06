using TradeSmart.Domain.Entities;

namespace TradeSmart.Domain.Interfaces.Proxies;

/// <summary>Proxy for sending trade notifications to Discord via webhook.</summary>
public interface IDiscordProxy
{
	/// <summary>Sends a notification for every incoming webhook signal showing what was received and what decision was made.</summary>
	Task<ProxyResponse<bool>> SendSignalReceivedNotificationAsync(
		TradingViewAlert alert,
		string decision,
		string? details = null,
		CancellationToken cancellationToken = default);

	/// <summary>Sends a trade analysis notification to the configured Discord channel.</summary>
	Task<ProxyResponse<bool>> SendTradeNotificationAsync(
		TradingViewAlert alert,
		TradeAnalysis analysis,
		CancellationToken cancellationToken = default);

	/// <summary>Sends a notification when a trade is opened.</summary>
	Task<ProxyResponse<bool>> SendTradeOpenedNotificationAsync(
		PaperPosition position,
		PaperWallet wallet,
		CancellationToken cancellationToken = default);

	/// <summary>Sends a notification when a trade is closed.</summary>
	Task<ProxyResponse<bool>> SendTradeClosedNotificationAsync(
		PaperPosition closedPosition,
		PaperWallet wallet,
		CancellationToken cancellationToken = default);
}
