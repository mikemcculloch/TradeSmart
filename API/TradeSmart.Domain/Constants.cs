namespace TradeSmart.Domain;

/// <summary>Domain-wide constants.</summary>
public static class Constants
{
	public const string CLAUDE_HTTP_CLIENT_NAME = "ClaudeHttpClient";
	public const string TWELVE_DATA_HTTP_CLIENT_NAME = "TwelveDataHttpClient";

	public static class ErrorCodes
	{
		public const int INVALID_INPUT = 1000;
		public const int EXTERNAL_API_ERROR = 1001;
		public const int CLAUDE_API_ERROR = 1002;
		public const int TWELVE_DATA_API_ERROR = 1003;
		public const int WEBHOOK_AUTH_FAILED = 1004;
	}

	public static class Timeframes
	{
		public const string ONE_MINUTE = "1min";
		public const string FIVE_MINUTES = "5min";
		public const string FIFTEEN_MINUTES = "15min";
		public const string ONE_HOUR = "1h";
		public const string FOUR_HOURS = "4h";
		public const string ONE_DAY = "1day";

		public static readonly string[] DEFAULT_TIMEFRAMES =
		[
			ONE_MINUTE,
			FIVE_MINUTES,
			FIFTEEN_MINUTES,
			ONE_HOUR,
			FOUR_HOURS,
			ONE_DAY
		];
	}
}
