namespace PactEf.Verify.Verification;

public sealed class VerificationResult
{
    public bool Success { get; private init; }
    public string? ErrorMessage { get; private init; }
    public string? PostgresErrorCode { get; private init; }
    public string? ParameterName { get; private init; }
    public string? VariantKind { get; private init; }
    public int? TestedLength { get; private init; }
    public int? ConsumerMaxLength { get; private init; }
    public int? DatabaseMaxLength { get; private init; }

    public static VerificationResult Ok() => new() { Success = true };

    public static VerificationResult Fail(
        string message,
        string? errorCode = null,
        string? parameterName = null,
        string? variantKind = null,
        int? testedLength = null,
        int? consumerMaxLength = null,
        int? databaseMaxLength = null) =>
        new()
        {
            Success = false,
            ErrorMessage = message,
            PostgresErrorCode = errorCode,
            ParameterName = parameterName,
            VariantKind = variantKind,
            TestedLength = testedLength,
            ConsumerMaxLength = consumerMaxLength,
            DatabaseMaxLength = databaseMaxLength
        };
}
