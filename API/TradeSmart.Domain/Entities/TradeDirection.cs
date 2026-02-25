namespace TradeSmart.Domain.Entities;

/// <summary>Represents the direction of a trade recommendation.</summary>
public enum TradeDirection
{
	/// <summary>No trade signal detected.</summary>
	NoTrade = 0,

	/// <summary>Long (buy) entry.</summary>
	Long = 1,

	/// <summary>Short (sell) entry.</summary>
	Short = 2
}
