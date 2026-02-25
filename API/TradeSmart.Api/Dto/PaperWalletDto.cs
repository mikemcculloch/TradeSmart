namespace TradeSmart.Api.Dto;

/// <summary>Paper wallet response.</summary>
public sealed record PaperWalletDto
{
	public decimal InitialBalance { get; init; }
	public decimal AvailableBalance { get; init; }
	public decimal TotalRealizedPnl { get; init; }
	public int TotalTrades { get; init; }
	public int WinningTrades { get; init; }
	public int LosingTrades { get; init; }
	public decimal WinRate => TotalTrades > 0 ? (decimal)WinningTrades / TotalTrades * 100 : 0;
}
