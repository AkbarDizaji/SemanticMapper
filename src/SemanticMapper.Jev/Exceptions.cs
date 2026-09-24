using System.Net;

namespace SemanticMapper.Jev;

/// <summary>The base type for TypeSafe Jev provider failures.</summary>
public class JevException : SemanticMatcherException
{
    /// <summary>Initializes a new instance.</summary>
    public JevException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public JevException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public JevException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when no TypeSafe API key is configured, or when TypeSafe rejects the key (HTTP 401/403).
/// </summary>
public class JevAuthenticationException : JevException
{
    /// <summary>Initializes a new instance.</summary>
    public JevAuthenticationException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public JevAuthenticationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public JevAuthenticationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance for an HTTP response.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="statusCode">The HTTP status code, if the key was rejected by the API.</param>
    /// <param name="requestId">The TypeSafe request id, if available.</param>
    public JevAuthenticationException(string message, HttpStatusCode? statusCode, string? requestId)
        : base(message)
    {
        StatusCode = statusCode;
        RequestId = requestId;
    }

    /// <summary>Gets the HTTP status code, or <see langword="null"/> when no key was configured.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>Gets the TypeSafe request id, if available.</summary>
    public string? RequestId { get; }
}

/// <summary>Thrown when the TypeSafe API returns an error or an unexpected response.</summary>
public class JevApiException : JevException
{
    /// <summary>Initializes a new instance.</summary>
    public JevApiException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public JevApiException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public JevApiException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance for an HTTP response.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="requestId">The TypeSafe request id, if available.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public JevApiException(string message, HttpStatusCode? statusCode, string? requestId, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        RequestId = requestId;
    }

    /// <summary>Gets the HTTP status code, or <see langword="null"/> when no response was received.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>Gets the TypeSafe request id, if available.</summary>
    public string? RequestId { get; }

    /// <summary>Gets a value indicating whether retrying later may succeed (rate limiting, overload, server errors).</summary>
    public bool IsTransient => StatusCode is null or HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int?)StatusCode >= 500;
}
