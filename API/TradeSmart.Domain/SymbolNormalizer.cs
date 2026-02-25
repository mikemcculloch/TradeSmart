using System.Text.RegularExpressions;

namespace TradeSmart.Domain;

/// <summary>Normalizes exchange-specific ticker symbols to Twelve Data compatible format.</summary>
public static partial class SymbolNormalizer
{
	/// <summary>
	/// Converts exchange-specific symbols (e.g., XRPUSDT.P from Bybit, BTCUSDT from Binance)
	/// to Twelve Data compatible format (e.g., XRP/USD).
	/// </summary>
	/// <param name="symbol">The raw symbol from TradingView.</param>
	/// <returns>The normalized symbol for Twelve Data.</returns>
	public static string Normalize(string symbol)
	{
		if (string.IsNullOrWhiteSpace(symbol))
		{
			return symbol;
		}

		var normalized = symbol.Trim().ToUpperInvariant();

		// Remove exchange-specific suffixes like .P (perpetual), .S (spot), etc.
		normalized = DotSuffixRegex().Replace(normalized, string.Empty);

		// Convert USDT pairs to USD (Twelve Data uses USD, not USDT)
		// e.g., XRPUSDT → XRP/USD, BTCUSDT → BTC/USD, ETHUSDT → ETH/USD
		if (normalized.EndsWith("USDT", StringComparison.Ordinal))
		{
			var baseAsset = normalized[..^4];
			return $"{baseAsset}/USD";
		}

		// Convert BUSD pairs to USD
		if (normalized.EndsWith("BUSD", StringComparison.Ordinal))
		{
			var baseAsset = normalized[..^4];
			return $"{baseAsset}/USD";
		}

		// Convert USD pairs without slash (e.g., BTCUSD → BTC/USD)
		// Only apply to crypto-like symbols (3-5 char base + USD)
		if (normalized.EndsWith("USD", StringComparison.Ordinal) && normalized.Length >= 6)
		{
			var baseAsset = normalized[..^3];

			// Only add slash if the base looks like a crypto ticker (2-5 uppercase letters)
			if (baseAsset.Length is >= 2 and <= 5 && CryptoBaseRegex().IsMatch(baseAsset))
			{
				return $"{baseAsset}/USD";
			}
		}

		return normalized;
	}

	[GeneratedRegex(@"\.[A-Z]+$")]
	private static partial Regex DotSuffixRegex();

	[GeneratedRegex(@"^[A-Z]{2,5}$")]
	private static partial Regex CryptoBaseRegex();
}
