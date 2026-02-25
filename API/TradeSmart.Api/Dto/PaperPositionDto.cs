namespace TradeSmart.Api.Dto;

/// <summary>Paper position response.</summary>
public sealed record PaperPositionDto
{
	public string PositionId { get; init; } = string.Empty;
	public string Symbol { get; init; } = string.Empty;
	public string Direction { get; init; } = string.Empty;
	public decimal EntryPrice { get; init; }
	public decimal PositionSizeUsd { get; init; }
	public decimal Quantity { get; init; }
	public decimal Leverage { get; init; }
	public decimal StopLoss { get; init; }
	public decimal TakeProfit { get; init; }
	public int Confidence { get; init; }
	public DateTimeOffset OpenedAt { get; init; }
	public DateTimeOffset? ClosedAt { get; init; }
	public decimal? ExitPrice { get; init; }
	public decimal? RealizedPnl { get; init; }
	public string? CloseReason { get; init; }
}
