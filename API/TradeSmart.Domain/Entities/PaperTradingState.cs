namespace TradeSmart.Domain.Entities;

/// <summary>Complete paper trading state for persistence.</summary>
public sealed record PaperTradingState
{
	/// <summary>The wallet state.</summary>
	public PaperWallet Wallet { get; init; } = new();

	/// <summary>Currently open positions.</summary>
	public IReadOnlyList<PaperPosition> OpenPositions { get; init; } = [];

	/// <summary>Closed position history.</summary>
	public IReadOnlyList<PaperPosition> ClosedPositions { get; init; } = [];

	/// <summary>UTC timestamp of last state update.</summary>
	public DateTimeOffset LastUpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
