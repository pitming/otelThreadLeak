using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;

namespace Evs.Phoenix.Utils.OpenTelemetry.Test.App;

public static class OpenTelemetryExtensions
{
  public static IServiceCollection AddOpenTelemetry(this IServiceCollection serviceCollection, IConfiguration configuration, string serviceName)
  {
    var openTelemetryConfiguration = configuration.Get<OpenTelemetryConfiguration>();

    if (openTelemetryConfiguration is null)
    {
      return serviceCollection;
    }

    serviceCollection.Configure<OpenTelemetryConfiguration>(configuration);
    serviceCollection.AddSingleton<OpenTelemetryConfigurationMonitor>();

    serviceCollection.AddLogging(logging =>
    {
      logging.AddOpenTelemetry(options =>
      {
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;

        var resBuilder = ResourceBuilder.CreateDefault();
        resBuilder.AddService(serviceName);
        options.SetResourceBuilder(resBuilder);
        options.AddOtlpExporter(p => p.Endpoint = openTelemetryConfiguration.ExporterEndpoint);
      });
    });
    return serviceCollection;
  }
}