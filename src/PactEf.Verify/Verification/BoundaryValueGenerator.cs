using PactEf.Core.Models;

namespace PactEf.Verify.Verification;

internal enum BoundaryValueKind
{
    MaxLength,
    Null
}

/// <summary>
/// Where a MaxLength boundary bound came from: declared on the consumer's captured
/// parameter metadata, or discovered by querying the live database schema when the
/// consumer didn't declare one. Database-sourced bounds are a schema capability, not a
/// proven consumer contract.
/// </summary>
internal enum BoundLengthSource
{
    Consumer,
    Database
}

internal sealed record BoundaryValue(BoundaryValueKind Kind, string Literal, BoundLengthSource? Source = null, int? Length = null);

internal static class BoundaryValueGenerator
{
    /// <param name="discoveredMaxLength">
    /// Database-discovered column length, used only when <paramref name="parameter"/> has no
    /// consumer-declared MaxLength.
    /// </param>
    public static IReadOnlyList<BoundaryValue> Generate(ParameterMetadata parameter, int? discoveredMaxLength = null)
    {
        var variants = new List<BoundaryValue>();

        if (parameter.MaxLength is int maxLength && maxLength > 0)
        {
            variants.Add(new BoundaryValue(
                BoundaryValueKind.MaxLength, $"'{new string('A', maxLength)}'", BoundLengthSource.Consumer, maxLength));
        }
        else if (discoveredMaxLength is int dbLength && dbLength > 0)
        {
            variants.Add(new BoundaryValue(
                BoundaryValueKind.MaxLength, $"'{new string('A', dbLength)}'", BoundLengthSource.Database, dbLength));
        }

        if (parameter.IsNullable == true)
        {
            variants.Add(new BoundaryValue(BoundaryValueKind.Null, "null"));
        }

        return variants;
    }
}
