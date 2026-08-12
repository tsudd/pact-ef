using System.Text;

namespace PactEf.Verify;

public sealed record QueryFailure(
    string ConsumerName,
    string Sql,
    string? TestName,
    string ErrorMessage,
    string? ErrorCode,
    string? CapturedSchemaVersion,
    string? CurrentSchemaVersion,
    string? ParameterName = null,
    string? VariantKind = null,
    int? TestedLength = null,
    int? ConsumerMaxLength = null,
    int? DatabaseMaxLength = null);

public static class FailureReport
{
    private const int ShortenedSqlLength = 80;

    public static string Format(IReadOnlyList<QueryFailure> failures)
    {
        var sb = new StringBuilder();
        foreach (var group in failures.GroupBy(f => f.ConsumerName))
        {
            sb.AppendLine($"FAILED  {group.Key}");
            foreach (var failure in group)
            {
                if (failure.TestName is not null)
                    sb.AppendLine($"  [{failure.TestName}]");

                var shortSql = failure.Sql.Length > ShortenedSqlLength
                    ? failure.Sql[..ShortenedSqlLength] + "..."
                    : failure.Sql;
                sb.AppendLine($"    {shortSql}");
                sb.AppendLine($"    ERROR: {failure.ErrorMessage}" +
                    (failure.ErrorCode is not null ? $" ({failure.ErrorCode})" : ""));

                if (failure.CapturedSchemaVersion is not null || failure.CurrentSchemaVersion is not null)
                    sb.AppendLine($"    Schema captured against: {failure.CapturedSchemaVersion ?? "unknown"} → current: {failure.CurrentSchemaVersion ?? "unknown"}");

                if (failure.ParameterName is not null)
                {
                    sb.AppendLine($"    Parameter: {failure.ParameterName} ({failure.VariantKind ?? "unknown"} variant, tested length: {failure.TestedLength?.ToString() ?? "unspecified"})");
                    sb.AppendLine($"    Database constraint discovered: {failure.DatabaseMaxLength?.ToString() ?? "unspecified"} / Consumer constraint: {failure.ConsumerMaxLength?.ToString() ?? "unspecified"}");
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
