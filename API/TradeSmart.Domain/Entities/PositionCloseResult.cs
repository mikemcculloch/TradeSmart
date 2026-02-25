namespace TradeSmart.Domain.Entities;

/// <summary>Result of closing a paper position.</summary>
public sealed record PositionCloseResult
{
	/// <summary>The closed position with final PnL.</summary>
	public PaperPosition ClosedPosition { get; init; } = null!;

	/// <summary>Updated wallet state after closing.</summary>
	public PaperWallet UpdatedWallet { get; init; } = null!;
}
