namespace TasmanianDevil.Azure;

/// <summary>
/// Thrown when the Azure AI Language service reports a per-document error. These come back on an HTTP
/// 200 response (in <c>results.errors</c>) rather than as a transport failure, so they would otherwise
/// be invisible; surfacing them as an exception routes them through the recognizer's fail-open/fail-closed
/// handling (and <see cref="AzurePiiOptions.OnError"/>) like any other failure.
/// </summary>
public sealed class AzurePiiException : Exception
{
    /// <summary>The Azure error code, e.g. <c>UnsupportedLanguageCode</c> or <c>InvalidDocument</c>.</summary>
    public string? Code { get; }

    /// <summary>Initializes a new instance of the <see cref="AzurePiiException"/> class.</summary>
    public AzurePiiException(string? code, string? message)
        : base(message ?? "Azure AI Language returned a document error.")
    {
        Code = code;
    }
}
