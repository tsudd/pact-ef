using Xunit.Sdk;

namespace PactEf.Capture.TestContext;

/// <summary>
/// Optional class-level attribute. When applied to a test class, each test method's
/// name is automatically set on PactEfTestContext.Current so captured queries
/// include testName/testClass in the snapshot.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class UsePactEfCaptureAttribute : BeforeAfterTestAttribute
{
    public override void Before(System.Reflection.MethodInfo methodUnderTest)
    {
        PactEfTestContext.Current = methodUnderTest.Name;
        PactEfTestContext.CurrentClass = methodUnderTest.DeclaringType?.FullName;
    }

    public override void After(System.Reflection.MethodInfo methodUnderTest)
    {
        PactEfTestContext.Current = null;
        PactEfTestContext.CurrentClass = null;
    }
}
