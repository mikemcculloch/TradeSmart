namespace TradeSmart.Domain.Entities;

/// <summary>Incoming alert payload from TradingView webhook.</summary>
public sealed record TradingViewAlert
{
	/// <summary>The ticker symbol (e.g., "AAPL", "BTCUSD").</summary>
	public string Symbol { get; init; } = string.Empty;

	/// <summary>The exchange (e.g., "NASDAQ", "BINANCE").</summary>
	public string Exchange { get; init; } = string.Empty;

	/// <summary>The alert action text from TradingView (e.g., "buy", "sell").</summary>
	public string Action { get; init; } = string.Empty;

	/// <summary>The price at the time the alert fired.</summary>
	public decimal Price { get; init; }

	/// <summary>The timeframe/interval that triggered the alert (e.g., "15", "60", "D").</summary>
	public string Interval { get; init; } = string.Empty;

	/// <summary>The alert message/description from TradingView.</summary>
	public string Message { get; init; } = string.Empty;

	/// <summary>Signal type: "entry" for new positions, "close" for exits.</summary>
	public string Type { get; init; } = "entry";

	/// <summary>Trade direction: "long" or "short" (entry signals only).</summary>
	public string? Direction { get; init; }

	/// <summary>Stop-loss price from the strategy (entry signals only).</summary>
	public decimal? StopLoss { get; init; }

	/// <summary>Take-profit price from the strategy (entry signals only).</summary>
	public decimal? TakeProfit { get; init; }

	/// <summary>Shared secret for webhook authentication.</summary>
	public string? Secret { get; init; }

	/// <summary>UTC timestamp when the alert was received.</summary>
	public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>Whether this is an entry signal.</summary>
	public bool IsEntry => Type.Equals("entry", StringComparison.OrdinalIgnoreCase);

	/// <summary>Whether this is a close/exit signal.</summary>
	public bool IsClose => Type.Equals("close", StringComparison.OrdinalIgnoreCase);

	/// <summary>Parses the Direction string into a <see cref="TradeDirection"/> enum.</summary>
	public TradeDirection ParsedDirection => Direction?.ToLowerInvariant() switch
	{
		"long" => TradeDirection.Long,
		"short" => TradeDirection.Short,
		_ => TradeDirection.NoTrade
	};
}
