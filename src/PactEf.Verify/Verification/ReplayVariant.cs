using PactEf.Core.Models;

namespace PactEf.Verify.Verification;

internal enum ReplayVariantKind
{
    Baseline,
    BoundaryMaxLength,
    BoundaryNull
}

internal sealed record ReplayVariant(
    string Sql,
    ReplayVariantKind Kind,
    string? ParameterName,
    BoundLengthSource? BoundSource = null,
    int? TestedLength = null);

internal static class ReplayVariantMatrixBuilder
{
    /// <summary>
    /// Builds the baseline replay plus one variant per boundary value generated for each
    /// parameter (max-length string, null where nullable). Each variant substitutes only the
    /// target parameter with its boundary literal; all other parameters keep their default literal.
    /// </summary>
    /// <param name="discoveredMaxLengths">
    /// Database-discovered column lengths keyed by parameter index, used only for parameters
    /// with no consumer-declared MaxLength.
    /// </param>
    public static IReadOnlyList<ReplayVariant> Build(
        string sql,
        IReadOnlyList<ParameterMetadata> parameters,
        IReadOnlyDictionary<int, int>? discoveredMaxLengths = null)
    {
        var variants = new List<ReplayVariant>
        {
            new(ParameterSubstitutor.Substitute(sql, parameters), ReplayVariantKind.Baseline, null)
        };

        for (var i = 0; i < parameters.Count; i++)
        {
            var discoveredMaxLength = discoveredMaxLengths is not null && discoveredMaxLengths.TryGetValue(i, out var len)
                ? len
                : (int?)null;

            foreach (var boundary in BoundaryValueGenerator.Generate(parameters[i], discoveredMaxLength))
            {
                var overrides = new Dictionary<int, string> { [i] = boundary.Literal };
                var variantSql = ParameterSubstitutor.Substitute(sql, parameters, overrides);
                var kind = boundary.Kind == BoundaryValueKind.MaxLength
                    ? ReplayVariantKind.BoundaryMaxLength
                    : ReplayVariantKind.BoundaryNull;

                variants.Add(new ReplayVariant(variantSql, kind, parameters[i].Name, boundary.Source, boundary.Length));
            }
        }

        return variants;
    }
}
