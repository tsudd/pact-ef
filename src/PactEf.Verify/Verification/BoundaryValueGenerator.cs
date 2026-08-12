using PactEf.Core.Models;

namespace PactEf.Verify.Verification;

internal enum BoundaryValueKind
{
    MaxLength,
    Null
}

internal sealed record BoundaryValue(BoundaryValueKind Kind, string Literal);

internal static class BoundaryValueGenerator
{
    public static IReadOnlyList<BoundaryValue> Generate(ParameterMetadata parameter)
    {
        var variants = new List<BoundaryValue>();

        if (parameter.MaxLength is int maxLength && maxLength > 0)
        {
            variants.Add(new BoundaryValue(BoundaryValueKind.MaxLength, $"'{new string('A', maxLength)}'"));
        }

        if (parameter.IsNullable == true)
        {
            variants.Add(new BoundaryValue(BoundaryValueKind.Null, "null"));
        }

        return variants;
    }
}
