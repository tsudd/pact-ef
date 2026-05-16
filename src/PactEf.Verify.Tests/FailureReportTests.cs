using PactEf.Verify;
using PactEf.Verify.Verification;

namespace PactEf.Verify.Tests;

public class FailureReportTests
{
    [Fact]
    public void Format_WithTestName_IncludesTestName()
    {
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

        var report = FailureReport.Format(failures);
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

        var report = FailureReport.Format(failures);
        Assert.Contains("OrderService", report);
        Assert.DoesNotContain("[", report); // no [TestName] bracket
    }
}
