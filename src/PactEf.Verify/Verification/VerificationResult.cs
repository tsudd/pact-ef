namespace PactEf.Verify.Verification;

public sealed class VerificationResult
{
    public bool Success { get; private init; }
    public string? ErrorMessage { get; private init; }
    public string? PostgresErrorCode { get; private init; }

    public static VerificationResult Ok() => new() { Success = true };

    public static VerificationResult Fail(string message, string? errorCode = null) =>
        new() { Success = false, ErrorMessage = message, PostgresErrorCode = errorCode };
}
