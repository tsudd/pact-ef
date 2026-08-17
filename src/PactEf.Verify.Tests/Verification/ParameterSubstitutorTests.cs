using PactEf.Core.Models;
using PactEf.Verify.Verification;

namespace PactEf.Verify.Tests.Verification;

public class ParameterSubstitutorTests
{
    [Theory]
    [InlineData("Int32", "0")]
    [InlineData("Int64", "0")]
    [InlineData("Int16", "0")]
    [InlineData("String", "''")]
    [InlineData("AnsiString", "''")]
    [InlineData("Boolean", "false")]
    [InlineData("Guid", "'00000000-0000-0000-0000-000000000000'")]
    [InlineData("DateTime", "'2000-01-01'")]
    [InlineData("Date", "'2000-01-01'")]
    [InlineData("Decimal", "0.0")]
    [InlineData("Currency", "0.0")]
    [InlineData("Object", "null")]
    public void GetLiteral_ReturnsExpectedLiteral(string dbType, string expected)
    {
        // Act
        var result = ParameterSubstitutor.GetLiteral(dbType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Substitute_ReplacesParameterPlaceholders()
    {
        // Arrange
        var sql = "SELECT * FROM \"Orders\" WHERE \"Id\" = $1 AND \"Status\" = $2";
        var parameters = new[]
        {
            new ParameterMetadata { ClrType = "Int32" },
            new ParameterMetadata { ClrType = "String" }
        };

        // Act
        var result = ParameterSubstitutor.Substitute(sql, parameters);

        // Assert
        Assert.Equal("SELECT * FROM \"Orders\" WHERE \"Id\" = 0 AND \"Status\" = ''", result);
    }

    [Fact]
    public void Substitute_ReplacesNpgsqlNamedPlaceholders()
    {
        // Arrange
        var sql = "SELECT o.\"Id\", o.\"Status\" FROM \"Orders\" AS o WHERE o.\"Id\" = @__id_0 LIMIT 1";
        var parameters = new[] { new ParameterMetadata { ClrType = "Int32" } };

        // Act
        var result = ParameterSubstitutor.Substitute(sql, parameters);

        // Assert
        Assert.Equal("SELECT o.\"Id\", o.\"Status\" FROM \"Orders\" AS o WHERE o.\"Id\" = 0 LIMIT 1", result);
    }

    [Fact]
    public void Substitute_ReplacesNpgsqlInsertPlaceholders()
    {
        // Arrange
        var sql = "INSERT INTO \"Orders\" (\"CreatedAt\", \"Status\")\nVALUES (@p0, @p1)\nRETURNING \"Id\";\n";
        var parameters = new[]
        {
            new ParameterMetadata { ClrType = "DateTime" },
            new ParameterMetadata { ClrType = "String" }
        };

        // Act
        var result = ParameterSubstitutor.Substitute(sql, parameters);

        // Assert
        Assert.Equal("INSERT INTO \"Orders\" (\"CreatedAt\", \"Status\")\nVALUES ('2000-01-01', '')\nRETURNING \"Id\";\n", result);
    }

    [Fact]
    public void Substitute_NoParameters_ReturnsSqlUnchanged()
    {
        // Arrange
        var sql = "SELECT * FROM \"Orders\"";

        // Act
        var result = ParameterSubstitutor.Substitute(sql, []);

        // Assert
        Assert.Equal(sql, result);
    }

    [Fact]
    public void Substitute_LegacyParameterMetadata_ClrTypeOnly_UsesDefaultLiteral()
    {
        // Arrange: metadata projected from a legacy v1 snapshot only carries ClrType
        var sql = "SELECT * FROM \"Orders\" WHERE \"Id\" = @__id_0";
        var parameters = new[] { new ParameterMetadata { ClrType = "Int32" } };

        // Act
        var result = ParameterSubstitutor.Substitute(sql, parameters);

        // Assert
        Assert.Equal("SELECT * FROM \"Orders\" WHERE \"Id\" = 0", result);
    }

    [Fact]
    public void Substitute_ParametersOutOfSqlOrder_MatchesPlaceholdersByName()
    {
        // Arrange: EF Core emits the UPDATE condition parameter (@p1) first in
        // DbCommand.Parameters, so the metadata order does not match order of appearance.
        var sql = "UPDATE \"OrderItems\" SET \"Description\" = @p0\nWHERE \"Id\" = @p1;\n";
        var parameters = new[]
        {
            new ParameterMetadata { Name = "@p1", ClrType = "Int32" },
            new ParameterMetadata { Name = "@p0", ClrType = "String" }
        };

        // Act
        var result = ParameterSubstitutor.Substitute(sql, parameters);

        // Assert
        Assert.Equal("UPDATE \"OrderItems\" SET \"Description\" = ''\nWHERE \"Id\" = 0;\n", result);
    }

    [Fact]
    public void Substitute_ValueOverride_ParametersOutOfSqlOrder_AppliesToNamedPlaceholder()
    {
        // Arrange: the boundary literal for the string parameter must land in the
        // "Description" slot, not in the integer "Id" slot.
        var sql = "UPDATE \"OrderItems\" SET \"Description\" = @p0\nWHERE \"Id\" = @p1;\n";
        var parameters = new[]
        {
            new ParameterMetadata { Name = "@p1", ClrType = "Int32" },
            new ParameterMetadata { Name = "@p0", ClrType = "String", MaxLength = 3 }
        };
        var overrides = new Dictionary<int, string> { [1] = "'AAA'" };

        // Act
        var result = ParameterSubstitutor.Substitute(sql, parameters, overrides);

        // Assert
        Assert.Equal("UPDATE \"OrderItems\" SET \"Description\" = 'AAA'\nWHERE \"Id\" = 0;\n", result);
    }

    [Fact]
    public void Substitute_RepeatedPlaceholder_UsesSameParameterMetadata()
    {
        // Arrange
        var sql = "SELECT * FROM \"Orders\" WHERE \"Id\" = @p0 OR \"ParentId\" = @p0";
        var parameters = new[] { new ParameterMetadata { Name = "@p0", ClrType = "Int32" } };

        // Act
        var result = ParameterSubstitutor.Substitute(sql, parameters);

        // Assert
        Assert.Equal("SELECT * FROM \"Orders\" WHERE \"Id\" = 0 OR \"ParentId\" = 0", result);
    }

    [Fact]
    public void Substitute_UnnamedParameters_FallsBackToOrderOfAppearance()
    {
        // Arrange: legacy v1 snapshots carry no parameter names
        var sql = "INSERT INTO \"Orders\" (\"CreatedAt\", \"Status\")\nVALUES (@p0, @p1)\nRETURNING \"Id\";\n";
        var parameters = new[]
        {
            new ParameterMetadata { ClrType = "DateTime" },
            new ParameterMetadata { ClrType = "String" }
        };

        // Act
        var result = ParameterSubstitutor.Substitute(sql, parameters);

        // Assert
        Assert.Equal("INSERT INTO \"Orders\" (\"CreatedAt\", \"Status\")\nVALUES ('2000-01-01', '')\nRETURNING \"Id\";\n", result);
    }

    [Fact]
    public void Substitute_ValueOverride_UsesCallerSuppliedLiteralInsteadOfDefault()
    {
        // Arrange
        var sql = "SELECT * FROM \"Orders\" WHERE \"Name\" = @p0 AND \"Id\" = @p1";
        var parameters = new[]
        {
            new ParameterMetadata { ClrType = "String", MaxLength = 10 },
            new ParameterMetadata { ClrType = "Int32" }
        };
        var overrides = new Dictionary<int, string> { [0] = "'AAAAAAAAAA'" };

        // Act
        var result = ParameterSubstitutor.Substitute(sql, parameters, overrides);

        // Assert
        Assert.Equal("SELECT * FROM \"Orders\" WHERE \"Name\" = 'AAAAAAAAAA' AND \"Id\" = 0", result);
    }
}
