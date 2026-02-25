namespace TradeSmart.Api.Dto;

/// <summary>Paper trading state response.</summary>
public sealed record PaperTradingStateDto
{
	public PaperWalletDto Wallet { get; init; } = null!;
	public IReadOnlyList<PaperPositionDto> OpenPositions { get; init; } = [];
	public DateTimeOffset LastUpdatedAt { get; init; }
}
