namespace Evs.Phoenix.Utils.OpenTelemetry.Test.App;

//this is just a basic configuration class
public class OpenTelemetryConfiguration
{
  public bool EnableTracing { get; set; } = false;
  public bool EnableMetrics { get; set; } = false;
  public Uri? ExporterEndpoint { get; set; }

}
