using PactEf.Core.Models;

namespace PactEf.Verify.Verification;

internal enum ReplayVariantKind
{
    Baseline,
    BoundaryMaxLength,
    BoundaryNull
}

internal sealed record ReplayVariant(string Sql, ReplayVariantKind Kind, string? ParameterName);

internal static class ReplayVariantMatrixBuilder
{
    /// <summary>
    /// Builds the baseline replay plus one variant per boundary value generated for each
    /// parameter (max-length string, null where nullable). Each variant substitutes only the
    /// target parameter with its boundary literal; all other parameters keep their default literal.
    /// </summary>
    public static IReadOnlyList<ReplayVariant> Build(string sql, IReadOnlyList<ParameterMetadata> parameters)
    {
        var variants = new List<ReplayVariant>
        {
            new(ParameterSubstitutor.Substitute(sql, parameters), ReplayVariantKind.Baseline, null)
        };

        for (var i = 0; i < parameters.Count; i++)
        {
            foreach (var boundary in BoundaryValueGenerator.Generate(parameters[i]))
            {
                var overrides = new Dictionary<int, string> { [i] = boundary.Literal };
                var variantSql = ParameterSubstitutor.Substitute(sql, parameters, overrides);
                var kind = boundary.Kind == BoundaryValueKind.MaxLength
                    ? ReplayVariantKind.BoundaryMaxLength
                    : ReplayVariantKind.BoundaryNull;

                variants.Add(new ReplayVariant(variantSql, kind, parameters[i].Name));
            }
        }

        return variants;
    }
}
