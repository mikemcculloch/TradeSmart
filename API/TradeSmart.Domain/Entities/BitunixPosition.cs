namespace TradeSmart.Domain.Entities;

/// <summary>A pending (open) position on Bitunix futures.</summary>
public sealed record BitunixPosition
{
	/// <summary>Position ID from the exchange.</summary>
	public string PositionId { get; init; } = string.Empty;

	/// <summary>Trading pair symbol (e.g., "BTCUSDT").</summary>
	public string Symbol { get; init; } = string.Empty;

	/// <summary>Position side: "BUY" (long) or "SELL" (short).</summary>
	public string Side { get; init; } = string.Empty;

	/// <summary>Quantity held.</summary>
	public decimal Qty { get; init; }

	/// <summary>Average entry price.</summary>
	public decimal EntryPrice { get; init; }

	/// <summary>Current mark price.</summary>
	public decimal MarkPrice { get; init; }

	/// <summary>Unrealized PnL.</summary>
	public decimal UnrealizedPnl { get; init; }

	/// <summary>Leverage applied.</summary>
	public decimal Leverage { get; init; }
}
