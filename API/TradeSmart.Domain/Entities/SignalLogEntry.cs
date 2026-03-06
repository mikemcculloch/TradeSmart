namespace TradeSmart.Domain.Entities;

/// <summary>Records every incoming webhook signal with the decision that was made.</summary>
public sealed record SignalLogEntry
{
	/// <summary>Unique identifier.</summary>
	public string Id { get; init; } = Guid.NewGuid().ToString("N");

	/// <summary>When the signal was received (UTC).</summary>
	public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>Signal type: "entry" or "close".</summary>
	public string Type { get; init; } = string.Empty;

	/// <summary>Trading symbol.</summary>
	public string Symbol { get; init; } = string.Empty;

	/// <summary>Exchange name.</summary>
	public string Exchange { get; init; } = string.Empty;

	/// <summary>Trade direction: "long", "short", or null for closes.</summary>
	public string? Direction { get; init; }

	/// <summary>Price at time of signal.</summary>
	public decimal Price { get; init; }

	/// <summary>Timeframe/interval.</summary>
	public string Interval { get; init; } = string.Empty;

	/// <summary>Stop-loss from strategy (entry only).</summary>
	public decimal? StopLoss { get; init; }

	/// <summary>Take-profit from strategy (entry only).</summary>
	public decimal? TakeProfit { get; init; }

	/// <summary>What decision was made: "TRADE OPENED", "REJECTED", "POSITION CLOSED", "ERROR", etc.</summary>
	public string Decision { get; init; } = string.Empty;

	/// <summary>Details/reason for the decision.</summary>
	public string? Details { get; init; }

	/// <summary>Whether a trade was actually opened/closed.</summary>
	public bool Executed { get; init; }

	/// <summary>The trading mode at time of signal: "Paper" or "Live".</summary>
	public string TradingMode { get; init; } = "Paper";
}
