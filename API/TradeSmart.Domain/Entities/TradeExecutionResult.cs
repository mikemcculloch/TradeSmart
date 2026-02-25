namespace TradeSmart.Domain.Entities;

/// <summary>Result of a trade execution decision.</summary>
public sealed record TradeExecutionResult
{
	/// <summary>Whether a trade was opened.</summary>
	public bool TradeOpened { get; init; }

	/// <summary>The opened position, if any.</summary>
	public PaperPosition? Position { get; init; }

	/// <summary>Reason the trade was rejected, if applicable.</summary>
	public string? RejectionReason { get; init; }

	/// <summary>The original analysis that was evaluated.</summary>
	public TradeAnalysis Analysis { get; init; } = null!;
}
