using PactEf.Core.Models;
using PactEf.Verify.Verification;

namespace PactEf.Verify.Tests.Verification;

public class BoundaryValueGeneratorTests
{
    [Fact]
    public void Generate_WithMaxLength_ReturnsExactLengthStringLiteral()
    {
        var parameter = new ParameterMetadata { ClrType = "String", MaxLength = 5 };

        var variants = BoundaryValueGenerator.Generate(parameter);

        var maxLengthVariant = Assert.Single(variants, v => v.Kind == BoundaryValueKind.MaxLength);
        Assert.Equal("'AAAAA'", maxLengthVariant.Literal);
    }

    [Fact]
    public void Generate_Nullable_IncludesNullVariant()
    {
        var parameter = new ParameterMetadata { ClrType = "String", MaxLength = 3, IsNullable = true };

        var variants = BoundaryValueGenerator.Generate(parameter);

        Assert.Contains(variants, v => v.Kind == BoundaryValueKind.Null && v.Literal == "null");
    }

    [Fact]
    public void Generate_NotNullable_ExcludesNullVariant()
    {
        var parameter = new ParameterMetadata { ClrType = "String", MaxLength = 3, IsNullable = false };

        var variants = BoundaryValueGenerator.Generate(parameter);

        Assert.DoesNotContain(variants, v => v.Kind == BoundaryValueKind.Null);
    }

    [Fact]
    public void Generate_NoMaxLength_ReturnsNoBoundaryVariant()
    {
        var parameter = new ParameterMetadata { ClrType = "String", MaxLength = null, IsNullable = false };

        var variants = BoundaryValueGenerator.Generate(parameter);

        Assert.Empty(variants);
    }

    [Fact]
    public void Generate_NoMaxLengthButNullable_ReturnsOnlyNullVariant()
    {
        var parameter = new ParameterMetadata { ClrType = "String", MaxLength = null, IsNullable = true };

        var variants = BoundaryValueGenerator.Generate(parameter);

        var variant = Assert.Single(variants);
        Assert.Equal(BoundaryValueKind.Null, variant.Kind);
    }

    [Fact]
    public void Generate_ConsumerMaxLength_UsesConsumerSourceOverDiscovered()
    {
        var parameter = new ParameterMetadata { ClrType = "String", MaxLength = 5 };

        var variants = BoundaryValueGenerator.Generate(parameter, discoveredMaxLength: 100);

        var variant = Assert.Single(variants, v => v.Kind == BoundaryValueKind.MaxLength);
        Assert.Equal("'AAAAA'", variant.Literal);
        Assert.Equal(BoundLengthSource.Consumer, variant.Source);
    }

    [Fact]
    public void Generate_NoConsumerMaxLengthButDiscovered_UsesDiscoveredLengthWithDatabaseSource()
    {
        var parameter = new ParameterMetadata { ClrType = "String", MaxLength = null };

        var variants = BoundaryValueGenerator.Generate(parameter, discoveredMaxLength: 100);

        var variant = Assert.Single(variants, v => v.Kind == BoundaryValueKind.MaxLength);
        Assert.Equal($"'{new string('A', 100)}'", variant.Literal);
        Assert.Equal(BoundLengthSource.Database, variant.Source);
    }

    [Fact]
    public void Generate_NoConsumerMaxLengthAndNoDiscovered_ReturnsNoBoundaryVariant()
    {
        var parameter = new ParameterMetadata { ClrType = "String", MaxLength = null };

        var variants = BoundaryValueGenerator.Generate(parameter, discoveredMaxLength: null);

        Assert.Empty(variants);
    }

    [Fact]
    public void Generate_IsDeterministic_AcrossRuns()
    {
        var parameter = new ParameterMetadata { ClrType = "String", MaxLength = 8, IsNullable = true };

        var first = BoundaryValueGenerator.Generate(parameter);
        var second = BoundaryValueGenerator.Generate(parameter);

        Assert.Equal(first, second);
    }
}
