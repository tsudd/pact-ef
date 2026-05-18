namespace PactEf.Verify.Verification;

public interface IQueryVerifier
{
    Task<VerificationResult> VerifyAsync(
        string sql,
        IReadOnlyList<string> parameterTypes,
        VerificationMode mode,
        CancellationToken cancellationToken = default);
}
