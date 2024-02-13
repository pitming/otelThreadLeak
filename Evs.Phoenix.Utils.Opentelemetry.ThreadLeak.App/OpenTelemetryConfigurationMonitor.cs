using Microsoft.Extensions.Options;

namespace Evs.Phoenix.Utils.OpenTelemetry.Test.App;

//this is th emonitor to ensure the configuration change will be triggered while the service is running
public class OpenTelemetryConfigurationMonitor
{
  private static OpenTelemetryConfiguration _current = new();
  public OpenTelemetryConfiguration CurrentValue { get; private set; }

  public OpenTelemetryConfigurationMonitor(IOptionsMonitor<OpenTelemetryConfiguration> openTelemetryConfiguration, ILogger<OpenTelemetryConfigurationMonitor> logger)
  {
    CurrentValue = openTelemetryConfiguration.CurrentValue;
    _current = openTelemetryConfiguration.CurrentValue;
    openTelemetryConfiguration.OnChange((configuration) =>
    {
      CurrentValue = _current = configuration;
      logger.LogTrace($"Loaded {nameof(OpenTelemetryConfiguration)} {configuration.EnableTracing}: {_current.EnableTracing} {nameof(configuration.EnableMetrics)}: {_current.EnableMetrics} ");
    });

  }

  public static OpenTelemetryConfiguration GetCurrentValue() => _current;
}
