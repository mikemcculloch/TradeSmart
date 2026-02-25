namespace TradeSmart.Domain.Entities;

/// <summary>Represents an open or closed paper trading position.</summary>
public sealed record PaperPosition
{
	/// <summary>Unique identifier for this position.</summary>
	public string PositionId { get; init; } = Guid.NewGuid().ToString("N");

	/// <summary>The normalized trading symbol (e.g., "BTC/USD").</summary>
	public string Symbol { get; init; } = string.Empty;

	/// <summary>Trade direction: Long or Short.</summary>
	public TradeDirection Direction { get; init; }

	/// <summary>Entry price when the position was opened.</summary>
	public decimal EntryPrice { get; init; }

	/// <summary>Position size in USD (margin collateral, before leverage).</summary>
	public decimal PositionSizeUsd { get; init; }

	/// <summary>Quantity of the asset (PositionSizeUsd * Leverage / EntryPrice).</summary>
	public decimal Quantity { get; init; }

	/// <summary>Leverage multiplier applied.</summary>
	public decimal Leverage { get; init; }

	/// <summary>Stop-loss price.</summary>
	public decimal StopLoss { get; init; }

	/// <summary>Take-profit price.</summary>
	public decimal TakeProfit { get; init; }

	/// <summary>AI confidence level (0-100) at time of opening.</summary>
	public int Confidence { get; init; }

	/// <summary>UTC timestamp when the position was opened.</summary>
	public DateTimeOffset OpenedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>UTC timestamp when the position was closed. Null if still open.</summary>
	public DateTimeOffset? ClosedAt { get; init; }

	/// <summary>Exit price when the position was closed. Null if still open.</summary>
	public decimal? ExitPrice { get; init; }

	/// <summary>Realized profit/loss in USD. Null if still open.</summary>
	public decimal? RealizedPnl { get; init; }

	/// <summary>Reason the position was closed.</summary>
	public string? CloseReason { get; init; }

	/// <summary>The original trade analysis reasoning from Claude.</summary>
	public string Reasoning { get; init; } = string.Empty;
}
