namespace TradeSmart.Domain.Entities;

/// <summary>Bitunix futures account summary.</summary>
public sealed record BitunixAccountInfo
{
	/// <summary>Margin coin (e.g., "USDT").</summary>
	public string MarginCoin { get; init; } = "USDT";

	/// <summary>Available equity in the account.</summary>
	public decimal Available { get; init; }

	/// <summary>Frozen/locked margin.</summary>
	public decimal Frozen { get; init; }

	/// <summary>Total equity (available + unrealized PnL).</summary>
	public decimal Equity { get; init; }

	/// <summary>Unrealized profit/loss across all open positions.</summary>
	public decimal UnrealizedPnl { get; init; }
}
