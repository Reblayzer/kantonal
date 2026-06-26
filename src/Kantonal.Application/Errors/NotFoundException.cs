namespace Kantonal.Application.Errors;

/// <summary>A requested resource does not exist. Mapped to HTTP 404 in the Api layer.</summary>
public sealed class NotFoundException : Exception
{
    public string Code { get; }
    public NotFoundException(string code, string message) : base(message) => Code = code;
}
