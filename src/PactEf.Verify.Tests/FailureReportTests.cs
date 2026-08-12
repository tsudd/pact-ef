namespace PactEf.Verify.Tests;

public class FailureReportTests
{
    [Fact]
    public void Format_WithTestName_IncludesTestName()
    {
        // Arrange
        var failures = new List<QueryFailure>
        {
            new(
                ConsumerName: "OrderService",
                Sql: "SELECT \"Id\" FROM \"Orders\"",
                TestName: "Test_GetById",
                ErrorMessage: "column does not exist",
                ErrorCode: "42703",
                CapturedSchemaVersion: "20260512183045",
                CurrentSchemaVersion: "20260514091200")
        };

        // Act
        var report = FailureReport.Format(failures);

        // Assert
        Assert.Contains("OrderService", report);
        Assert.Contains("Test_GetById", report);
        Assert.Contains("column does not exist", report);
        Assert.Contains("42703", report);
        Assert.Contains("20260512183045", report);
        Assert.Contains("20260514091200", report);
    }

    [Fact]
    public void Format_WithoutTestName_OmitsTestNameLine()
    {
        // Arrange
        var failures = new List<QueryFailure>
        {
            new(
                ConsumerName: "OrderService",
                Sql: "SELECT \"Id\" FROM \"Orders\"",
                TestName: null,
                ErrorMessage: "relation does not exist",
                ErrorCode: "42P01",
                CapturedSchemaVersion: null,
                CurrentSchemaVersion: null)
        };

        // Act
        var report = FailureReport.Format(failures);

        // Assert
        Assert.Contains("OrderService", report);
        Assert.DoesNotContain("[", report); // no [TestName] bracket
    }

    [Fact]
    public void Format_WithBoundaryFailure_IncludesParameterVariantAndConstraintSources()
    {
        // Arrange
        var failures = new List<QueryFailure>
        {
            new(
                ConsumerName: "OrderService",
                Sql: "INSERT INTO \"OrderItems\" (\"Description\") VALUES (@p0)",
                TestName: "Test_InsertOrderItem",
                ErrorMessage: "value too long for type character varying(100)",
                ErrorCode: "22001",
                CapturedSchemaVersion: null,
                CurrentSchemaVersion: null,
                ParameterName: "p0",
                VariantKind: "boundary-max-length",
                TestedLength: 100,
                ConsumerMaxLength: null,
                DatabaseMaxLength: 100)
        };

        // Act
        var report = FailureReport.Format(failures);

        // Assert
        Assert.Contains("Parameter: p0 (boundary-max-length variant, tested length: 100)", report);
        Assert.Contains("Database constraint discovered: 100 / Consumer constraint: unspecified", report);
    }

    [Fact]
    public void Format_WithoutParameterInfo_OmitsParameterLines()
    {
        // Arrange
        var failures = new List<QueryFailure>
        {
            new(
                ConsumerName: "OrderService",
                Sql: "SELECT \"Id\" FROM \"Orders\"",
                TestName: null,
                ErrorMessage: "relation does not exist",
                ErrorCode: "42P01",
                CapturedSchemaVersion: null,
                CurrentSchemaVersion: null)
        };

        // Act
        var report = FailureReport.Format(failures);

        // Assert
        Assert.DoesNotContain("Parameter:", report);
        Assert.DoesNotContain("Database constraint discovered:", report);
    }
}
