using PactEf.Core.Models;

namespace PactEf.Verify.Verification;

public interface IQueryVerifier
{
    Task<VerificationResult> VerifyAsync(
        string sql,
        IReadOnlyList<ParameterMetadata> parameters,
        VerificationMode mode,
        CancellationToken cancellationToken = default);
}
