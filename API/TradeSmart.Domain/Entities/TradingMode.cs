namespace TradeSmart.Domain.Entities;

/// <summary>Determines whether trades execute on paper or on a live exchange.</summary>
public enum TradingMode
{
	/// <summary>Simulated trades tracked in memory / file-based wallet.</summary>
	Paper = 0,

	/// <summary>Real orders sent to Bitunix exchange.</summary>
	Live = 1
}
