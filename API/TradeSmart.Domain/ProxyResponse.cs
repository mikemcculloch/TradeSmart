namespace TradeSmart.Domain;

/// <summary>Standard response wrapper for all proxy and service operations.</summary>
/// <typeparam name="T">The result type.</typeparam>
public sealed class ProxyResponse<T>
{
	/// <summary>The successful result value.</summary>
	public T? Result { get; set; }

	/// <summary>Error details when the operation failed.</summary>
	public ResponseError? Error { get; set; }

	/// <summary>Whether the response contains errors.</summary>
	public bool HasErrors => Error is not null;

	/// <summary>Creates a successful response.</summary>
	/// <param name="result">The result value.</param>
	/// <returns>A successful <see cref="ProxyResponse{T}"/>.</returns>
	public static ProxyResponse<T> Success(T result)
	{
		return new ProxyResponse<T> { Result = result };
	}

	/// <summary>Creates an error response.</summary>
	/// <param name="code">The error code.</param>
	/// <param name="message">The error message.</param>
	/// <returns>An error <see cref="ProxyResponse{T}"/>.</returns>
	public static ProxyResponse<T> CreateError(int code, string message)
	{
		return new ProxyResponse<T>
		{
			Error = new ResponseError(code, message)
		};
	}
}

/// <summary>Represents an error in a proxy or service response.</summary>
/// <param name="Code">The error code.</param>
/// <param name="Message">The error message.</param>
public sealed record ResponseError(int Code, string Message);
