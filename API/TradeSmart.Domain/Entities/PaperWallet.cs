namespace TradeSmart.Domain.Entities;

/// <summary>Represents the paper trading wallet state.</summary>
public sealed record PaperWallet
{
	/// <summary>Starting balance in USD.</summary>
	public decimal InitialBalance { get; init; } = 1000m;

	/// <summary>Current available balance in USD (not locked in positions).</summary>
	public decimal AvailableBalance { get; init; } = 1000m;

	/// <summary>Total realized profit/loss across all closed positions.</summary>
	public decimal TotalRealizedPnl { get; init; }

	/// <summary>Total number of trades executed.</summary>
	public int TotalTrades { get; init; }

	/// <summary>Number of winning trades.</summary>
	public int WinningTrades { get; init; }

	/// <summary>Number of losing trades.</summary>
	public int LosingTrades { get; init; }
}
