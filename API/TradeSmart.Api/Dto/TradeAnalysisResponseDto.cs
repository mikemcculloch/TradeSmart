namespace TradeSmart.Api.Dto;

/// <summary>Trade analysis response returned to the caller.</summary>
public sealed record TradeAnalysisResponseDto
{
	/// <summary>The symbol analyzed.</summary>
	public string Symbol { get; init; } = string.Empty;

	/// <summary>Recommended trade direction: "Long", "Short", or "NoTrade".</summary>
	public string Direction { get; init; } = string.Empty;

	/// <summary>Confidence level from 0 to 100.</summary>
	public int Confidence { get; init; }

	/// <summary>Suggested entry price.</summary>
	public decimal? EntryPrice { get; init; }

	/// <summary>Suggested stop-loss price.</summary>
	public decimal? StopLoss { get; init; }

	/// <summary>Suggested take-profit price.</summary>
	public decimal? TakeProfit { get; init; }

	/// <summary>Risk-to-reward ratio (e.g., "1:2.5").</summary>
	public string? RiskRewardRatio { get; init; }

	/// <summary>Claude's reasoning for the recommendation.</summary>
	public string Reasoning { get; init; } = string.Empty;

	/// <summary>UTC timestamp of the analysis.</summary>
	public DateTimeOffset AnalyzedAt { get; init; }
}
