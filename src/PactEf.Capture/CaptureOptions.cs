namespace PactEf.Capture;

public sealed class CaptureOptions
{
    public required string ConsumerName { get; set; }
    public string DisableEnvVariable { get; set; } = "PACTEF_CAPTURE_DISABLED";
}
