using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TradeSmart.Infrastructure.Proxy;

/// <summary>
/// Manages the SQLite database connection and schema initialization.
/// Provides helper methods for creating connections and executing queries.
/// </summary>
public sealed class TradeDatabase : IDisposable
{
	private readonly string _connectionString;
	private readonly ILogger<TradeDatabase> _logger;
	private bool _initialized;

	public TradeDatabase(IConfiguration configuration, ILogger<TradeDatabase> logger)
	{
		var dbPath = configuration["Database:Path"] ?? "tradesmart.db";
		_connectionString = $"Data Source={dbPath}";
		_logger = logger;
	}

	/// <summary>Creates and returns an open SQLite connection.</summary>
	public async Task<SqliteConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
	{
		if (!_initialized)
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
		}

		var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		return connection;
	}

	private async Task InitializeAsync(CancellationToken cancellationToken)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Enable WAL mode for better concurrent read/write performance
		await using (var walCmd = connection.CreateCommand())
		{
			walCmd.CommandText = "PRAGMA journal_mode=WAL;";
			await walCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		await using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			CREATE TABLE IF NOT EXISTS signal_log (
				id TEXT PRIMARY KEY,
				received_at TEXT NOT NULL,
				type TEXT NOT NULL,
				symbol TEXT NOT NULL,
				exchange TEXT,
				direction TEXT,
				price REAL NOT NULL,
				interval TEXT,
				stop_loss REAL,
				take_profit REAL,
				decision TEXT NOT NULL,
				details TEXT,
				executed INTEGER NOT NULL DEFAULT 0,
				trading_mode TEXT NOT NULL DEFAULT 'Paper'
			);

			CREATE INDEX IF NOT EXISTS idx_signal_log_received_at ON signal_log(received_at DESC);
			CREATE INDEX IF NOT EXISTS idx_signal_log_symbol ON signal_log(symbol);

			CREATE TABLE IF NOT EXISTS wallet_state (
				id INTEGER PRIMARY KEY CHECK (id = 1),
				initial_balance REAL NOT NULL DEFAULT 1000,
				available_balance REAL NOT NULL DEFAULT 1000,
				total_realized_pnl REAL NOT NULL DEFAULT 0,
				total_trades INTEGER NOT NULL DEFAULT 0,
				winning_trades INTEGER NOT NULL DEFAULT 0,
				losing_trades INTEGER NOT NULL DEFAULT 0,
				updated_at TEXT NOT NULL
			);

			CREATE TABLE IF NOT EXISTS positions (
				position_id TEXT PRIMARY KEY,
				symbol TEXT NOT NULL,
				direction TEXT NOT NULL,
				entry_price REAL NOT NULL,
				position_size_usd REAL NOT NULL,
				quantity REAL NOT NULL,
				leverage REAL NOT NULL,
				stop_loss REAL NOT NULL,
				take_profit REAL NOT NULL,
				confidence INTEGER NOT NULL,
				reasoning TEXT,
				opened_at TEXT NOT NULL,
				closed_at TEXT,
				exit_price REAL,
				realized_pnl REAL,
				close_reason TEXT,
				is_open INTEGER NOT NULL DEFAULT 1
			);

			CREATE INDEX IF NOT EXISTS idx_positions_symbol ON positions(symbol);
			CREATE INDEX IF NOT EXISTS idx_positions_is_open ON positions(is_open);
			CREATE INDEX IF NOT EXISTS idx_positions_closed_at ON positions(closed_at DESC);
			""";

		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

		_logger.LogInformation("SQLite database initialized at {ConnectionString}", _connectionString);
		_initialized = true;
	}

	public void Dispose()
	{
		// SqliteConnection is opened/closed per-use; nothing to dispose at the pool level
	}
}
