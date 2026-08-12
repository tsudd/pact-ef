using PactEf.Core.Models;
using PactEf.Verify.Verification;

namespace PactEf.Verify.Tests.Verification;

public class ReplayVariantMatrixBuilderTests
{
    [Fact]
    public void Build_NoBoundaryMetadata_ReturnsOnlyBaseline()
    {
        var sql = "SELECT * FROM \"Orders\" WHERE \"Id\" = @p0";
        var parameters = new[] { new ParameterMetadata { ClrType = "Int32" } };

        var variants = ReplayVariantMatrixBuilder.Build(sql, parameters);

        var variant = Assert.Single(variants);
        Assert.Equal(ReplayVariantKind.Baseline, variant.Kind);
        Assert.Equal("SELECT * FROM \"Orders\" WHERE \"Id\" = 0", variant.Sql);
    }

    [Fact]
    public void Build_MaxLengthParameter_AddsBoundaryMaxLengthVariant()
    {
        var sql = "INSERT INTO \"Orders\" (\"Status\") VALUES (@p0)";
        var parameters = new[] { new ParameterMetadata { Name = "Status", ClrType = "String", MaxLength = 5 } };

        var variants = ReplayVariantMatrixBuilder.Build(sql, parameters);

        Assert.Equal(2, variants.Count);
        var maxLengthVariant = Assert.Single(variants, v => v.Kind == ReplayVariantKind.BoundaryMaxLength);
        Assert.Equal("INSERT INTO \"Orders\" (\"Status\") VALUES ('AAAAA')", maxLengthVariant.Sql);
        Assert.Equal("Status", maxLengthVariant.ParameterName);
    }

    [Fact]
    public void Build_NullableParameter_AddsBoundaryNullVariant()
    {
        var sql = "INSERT INTO \"OrderItems\" (\"Description\") VALUES (@p0)";
        var parameters = new[]
        {
            new ParameterMetadata { Name = "Description", ClrType = "String", IsNullable = true }
        };

        var variants = ReplayVariantMatrixBuilder.Build(sql, parameters);

        Assert.Equal(2, variants.Count);
        var nullVariant = Assert.Single(variants, v => v.Kind == ReplayVariantKind.BoundaryNull);
        Assert.Equal("INSERT INTO \"OrderItems\" (\"Description\") VALUES (null)", nullVariant.Sql);
    }

    [Fact]
    public void Build_MultipleParameters_OnlyOverridesTargetParameterPerVariant()
    {
        var sql = "INSERT INTO \"Orders\" (\"Status\", \"Note\") VALUES (@p0, @p1)";
        var parameters = new[]
        {
            new ParameterMetadata { Name = "Status", ClrType = "String", MaxLength = 3 },
            new ParameterMetadata { Name = "Note", ClrType = "Int32" }
        };

        var variants = ReplayVariantMatrixBuilder.Build(sql, parameters);

        var maxLengthVariant = Assert.Single(variants, v => v.Kind == ReplayVariantKind.BoundaryMaxLength);
        Assert.Equal("INSERT INTO \"Orders\" (\"Status\", \"Note\") VALUES ('AAA', 0)", maxLengthVariant.Sql);
    }

    [Fact]
    public void Build_NoConsumerMaxLengthWithDiscoveredLength_AddsBoundaryVariantFromDatabase()
    {
        var sql = "INSERT INTO \"OrderItems\" (\"Description\") VALUES (@p0)";
        var parameters = new[] { new ParameterMetadata { Name = "Description", ClrType = "String" } };

        var variants = ReplayVariantMatrixBuilder.Build(
            sql, parameters, discoveredMaxLengths: new Dictionary<int, int> { [0] = 100 });

        var maxLengthVariant = Assert.Single(variants, v => v.Kind == ReplayVariantKind.BoundaryMaxLength);
        Assert.Equal($"INSERT INTO \"OrderItems\" (\"Description\") VALUES ('{new string('A', 100)}')", maxLengthVariant.Sql);
        Assert.Equal(BoundLengthSource.Database, maxLengthVariant.BoundSource);
    }

    [Fact]
    public void Build_ConsumerMaxLength_ReportsConsumerSourceIgnoringDiscoveredLength()
    {
        var sql = "INSERT INTO \"Orders\" (\"Status\") VALUES (@p0)";
        var parameters = new[] { new ParameterMetadata { Name = "Status", ClrType = "String", MaxLength = 5 } };

        var variants = ReplayVariantMatrixBuilder.Build(
            sql, parameters, discoveredMaxLengths: new Dictionary<int, int> { [0] = 100 });

        var maxLengthVariant = Assert.Single(variants, v => v.Kind == ReplayVariantKind.BoundaryMaxLength);
        Assert.Equal("INSERT INTO \"Orders\" (\"Status\") VALUES ('AAAAA')", maxLengthVariant.Sql);
        Assert.Equal(BoundLengthSource.Consumer, maxLengthVariant.BoundSource);
    }

    [Fact]
    public void Build_IsDeterministic_AcrossRuns()
    {
        var sql = "INSERT INTO \"Orders\" (\"Status\") VALUES (@p0)";
        var parameters = new[]
        {
            new ParameterMetadata { Name = "Status", ClrType = "String", MaxLength = 5, IsNullable = true }
        };

        var first = ReplayVariantMatrixBuilder.Build(sql, parameters);
        var second = ReplayVariantMatrixBuilder.Build(sql, parameters);

        Assert.Equal(first, second);
    }
}
