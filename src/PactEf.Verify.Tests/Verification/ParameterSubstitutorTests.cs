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
        var types = new[] { "Int32", "String" };

        // Act
        var result = ParameterSubstitutor.Substitute(sql, types);

        // Assert
        Assert.Equal("SELECT * FROM \"Orders\" WHERE \"Id\" = 0 AND \"Status\" = ''", result);
    }

    [Fact]
    public void Substitute_ReplacesNpgsqlNamedPlaceholders()
    {
        // Arrange
        var sql = "SELECT o.\"Id\", o.\"Status\" FROM \"Orders\" AS o WHERE o.\"Id\" = @__id_0 LIMIT 1";
        var types = new[] { "Int32" };

        // Act
        var result = ParameterSubstitutor.Substitute(sql, types);

        // Assert
        Assert.Equal("SELECT o.\"Id\", o.\"Status\" FROM \"Orders\" AS o WHERE o.\"Id\" = 0 LIMIT 1", result);
    }

    [Fact]
    public void Substitute_ReplacesNpgsqlInsertPlaceholders()
    {
        // Arrange
        var sql = "INSERT INTO \"Orders\" (\"CreatedAt\", \"Status\")\nVALUES (@p0, @p1)\nRETURNING \"Id\";\n";
        var types = new[] { "DateTime", "String" };

        // Act
        var result = ParameterSubstitutor.Substitute(sql, types);

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
}
