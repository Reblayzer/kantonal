namespace Kantonal.Api;

public static class ApiEnvelope
{
    public static object Success(object data) => new { ok = true, data };
    public static object Error(string code, string message) => new { ok = false, error = new { code, message } };
}
