using System.ComponentModel.DataAnnotations;

namespace TradeSmart.Api.Dto;

/// <summary>Incoming webhook payload for signal-only strategies (no SL/TP).</summary>
public sealed record StrategySignalRequestDto
{
	/// <summary>The ticker symbol (e.g., "BTCUSD").</summary>
	[Required(ErrorMessage = "Symbol is required.")]
	public string Symbol { get; init; } = string.Empty;

	/// <summary>Signal type: "entry" for new positions, "close" for exits.</summary>
	[Required(ErrorMessage = "Type is required.")]
	public string Type { get; init; } = "entry";

	/// <summary>Trade direction: "long" or "short" (entry signals only).</summary>
	public string? Direction { get; init; }

	/// <summary>The price at the time the alert fired.</summary>
	public decimal Price { get; init; }

	/// <summary>The timeframe/interval that triggered the alert (e.g., "1D", "4H").</summary>
	public string Interval { get; init; } = string.Empty;

	/// <summary>The alert message/description from TradingView.</summary>
	public string Message { get; init; } = string.Empty;

	/// <summary>Shared secret for webhook authentication.</summary>
	public string? Secret { get; init; }
}
