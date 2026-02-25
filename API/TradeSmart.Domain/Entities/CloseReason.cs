namespace TradeSmart.Domain.Entities;

/// <summary>Reasons a paper position can be closed.</summary>
public static class CloseReason
{
	public const string StopLoss = "StopLoss";
	public const string TakeProfit = "TakeProfit";
	public const string Manual = "Manual";
}
