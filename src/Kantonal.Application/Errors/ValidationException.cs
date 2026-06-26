namespace Kantonal.Application.Errors;

/// <summary>Caller input failed validation. Mapped to HTTP 400 in the Api layer.</summary>
public sealed class ValidationException : Exception
{
    public string Code { get; }
    // NOTE: `message` is returned to API clients in the error envelope — keep it user-facing, no internal detail.
    public ValidationException(string code, string message) : base(message) => Code = code;
}
