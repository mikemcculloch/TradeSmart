namespace TradeSmart.Domain;

/// <summary>Domain-wide constants.</summary>
public static class Constants
{
	public const string CLAUDE_HTTP_CLIENT_NAME = "ClaudeHttpClient";
	public const string DISCORD_HTTP_CLIENT_NAME = "DiscordHttpClient";
	public const string TWELVE_DATA_HTTP_CLIENT_NAME = "TwelveDataHttpClient";

	public static class ErrorCodes
	{
		public const int INVALID_INPUT = 1000;
		public const int EXTERNAL_API_ERROR = 1001;
		public const int CLAUDE_API_ERROR = 1002;
		public const int TWELVE_DATA_API_ERROR = 1003;
		public const int WEBHOOK_AUTH_FAILED = 1004;
		public const int DISCORD_NOTIFICATION_ERROR = 1005;

		public const int PAPER_TRADING_ERROR = 2000;
		public const int PAPER_TRADING_STATE_ERROR = 2001;
		public const int POSITION_LIMIT_REACHED = 2002;
		public const int DUPLICATE_SYMBOL_POSITION = 2003;
		public const int INSUFFICIENT_BALANCE = 2004;
		public const int POSITION_NOT_FOUND = 2005;
		public const int INVALID_TRADE_PARAMETERS = 2006;
		public const int SYMBOL_NOT_ALLOWED = 2007;
	}

	public static class PaperTrading
	{
		public const decimal DEFAULT_INITIAL_BALANCE = 1000m;
		public const int DEFAULT_CONFIDENCE_THRESHOLD = 80;
		public const decimal DEFAULT_MAX_POSITION_SIZE_PERCENT = 0.10m;
		public const int DEFAULT_MAX_CONCURRENT_POSITIONS = 2;
		public const decimal DEFAULT_LEVERAGE = 2m;
		public const decimal DEFAULT_MAX_STOP_LOSS_PERCENT = 0.20m;
		public const int DEFAULT_MONITOR_INTERVAL_SECONDS = 60;

		/// <summary>Base symbols allowed for paper trading.</summary>
		public static readonly string[] ALLOWED_SYMBOLS = ["BTC", "XAU", "XAG", "XPT"];
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
