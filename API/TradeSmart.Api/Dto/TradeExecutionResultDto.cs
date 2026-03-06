namespace TradeSmart.Api.Dto;

/// <summary>Response DTO for trade execution results.</summary>
public sealed record TradeExecutionResultDto
{
	/// <summary>Whether a trade was opened.</summary>
	public bool TradeOpened { get; init; }

	/// <summary>The traded symbol.</summary>
	public string Symbol { get; init; } = string.Empty;

	/// <summary>Trade direction ("Long", "Short", or "NoTrade").</summary>
	public string Direction { get; init; } = string.Empty;

	/// <summary>The entry price, if a trade was opened.</summary>
	public decimal? EntryPrice { get; init; }

	/// <summary>Stop-loss level.</summary>
	public decimal? StopLoss { get; init; }

	/// <summary>Take-profit level.</summary>
	public decimal? TakeProfit { get; init; }

	/// <summary>Reason the trade was rejected, if applicable.</summary>
	public string? RejectionReason { get; init; }
}
