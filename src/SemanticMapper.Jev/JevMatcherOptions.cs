namespace SemanticMapper.Jev;

/// <summary>Options for <see cref="JevSemanticFieldMatcher"/>.</summary>
public sealed class JevMatcherOptions
{
    /// <summary>
    /// Gets or sets the TypeSafe API key. When not set, the configuration key <c>TypeSafe:ApiKey</c>
    /// and then the <c>TYPESAFE_API_KEY</c> environment variable are used.
    /// </summary>
    /// <remarks>The key is sent only to <see cref="BaseAddress"/> and is never logged.</remarks>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the TypeSafe API base address. Default <c>https://api.typesafe.ai</c>.</summary>
    public Uri BaseAddress { get; set; } = new("https://api.typesafe.ai");

    /// <summary>
    /// Gets or sets the Jev model. Default <c>jev-latest</c>. Pin a specific version (for example
    /// <c>jev-1.13.0</c>) when you have calibrated confidence thresholds; the model is part of the
    /// matcher version and therefore of the mapping-plan cache key.
    /// </summary>
    public string Model { get; set; } = "jev-latest";

    /// <summary>Gets or sets the timeout for a single API request. Default 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether the source value is sent to TypeSafe as additional context.
    /// Default <see langword="false"/>: only field names, paths and inferred kinds are sent, because
    /// values may contain sensitive data.
    /// </summary>
    public bool IncludeSourceValues { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"Model={Model}, BaseAddress={BaseAddress}, ApiKey={(string.IsNullOrEmpty(ApiKey) ? "(not set)" : "***")}";
}
