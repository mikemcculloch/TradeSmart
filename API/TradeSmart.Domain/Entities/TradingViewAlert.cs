namespace TradeSmart.Domain.Entities;

/// <summary>Incoming alert payload from TradingView webhook.</summary>
public sealed record TradingViewAlert
{
	/// <summary>The ticker symbol (e.g., "AAPL", "BTCUSD").</summary>
	public string Symbol { get; init; } = string.Empty;

	/// <summary>The exchange (e.g., "NASDAQ", "BINANCE").</summary>
	public string Exchange { get; init; } = string.Empty;

	/// <summary>The alert action text from TradingView (e.g., "buy", "sell", "strong_buy").</summary>
	public string Action { get; init; } = string.Empty;

	/// <summary>The price at the time the alert fired.</summary>
	public decimal Price { get; init; }

	/// <summary>The timeframe/interval that triggered the alert (e.g., "15", "60", "D").</summary>
	public string Interval { get; init; } = string.Empty;

	/// <summary>The alert message/description from TradingView.</summary>
	public string Message { get; init; } = string.Empty;

	/// <summary>Shared secret for webhook authentication.</summary>
	public string? Secret { get; init; }

	/// <summary>UTC timestamp when the alert was received.</summary>
	public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
}
