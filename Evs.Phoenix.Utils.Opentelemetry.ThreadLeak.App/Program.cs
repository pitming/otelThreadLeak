using System.Diagnostics;
using Microsoft.AspNetCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;

namespace Evs.Phoenix.Utils.OpenTelemetry.Test.App;

public class Program
{
  public static readonly ActivitySource source = new("OpenTelemtry.Test.App");
  public static async Task Main(string[] args)
  {
    const int port = 1819;
    Console.WriteLine("Et c'est partiiii");

   var configuration = new ConfigurationBuilder()
      .AddEnvironmentVariables()
    .Build();

    var host = WebHost.CreateDefaultBuilder(args)
      .UseConfiguration(configuration)
      .UseStartup<Startup>()
      .ConfigureLogging(logging =>
      {
        logging.ClearProviders();
        logging.AddConsole();
      })
      .ConfigureKestrel(options =>
      {
        options.ListenAnyIP(port);
      })
      .Build();

    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    var cli = new HttpClient
    {
      BaseAddress = new Uri($"http://localhost:{port}")
    };
    var configRoot = host.Services.GetRequiredService<IConfiguration>() as IConfigurationRoot;

    var _ = Task.Run(async () =>
    {
      while (true)
      {
        var response = await cli.GetAsync("/otel");
        logger.LogInformation($"/otel --> {response.StatusCode}");
        configRoot.Reload();
        await Task.Delay(250);
      }
    });

    await host.RunAsync().ConfigureAwait(false);
  }
}

public class Startup
{
  public static readonly ActivitySource source = new("otelThreadLeak.Test.App");

  private readonly ILoggerFactory _loggerFactory;

  private readonly IConfiguration _configuration;

  public Startup(IConfiguration configuration, ILoggerFactory loggerFactory)
  {
    _configuration = configuration;
    _loggerFactory = loggerFactory;
  }

  public void ConfigureServices(IServiceCollection services)
  {
    services.AddOpenTelemetry(_configuration, "otelThreadLeak"/*, builder => builder.AddSource("Evs.Phoenix.Data")*/);
  }

  public void Configure(IApplicationBuilder app, ILogger<Startup> logger)
  {
    app.UseRouting();

    app.UseEndpoints(endpoints =>
    {
      endpoints.MapGet("/otel", () =>
      {
        //create activity
        using var activity = source.StartActivity("otel.detail");

        //make some logging
        logger.LogInformation($"otel was called at {DateTime.Now}");

        return Thread.CurrentThread.ManagedThreadId;
      });
    });
  }
}

public class OpenTelemetryConfiguration
{
  public bool EnableTracing { get; set; } = false;
  public bool EnableMetrics { get; set; } = false;
  public Uri? ExporterEndpoint { get; set; }
  public string? HostName { get; set; }
  public bool AddMessageKafka { get; set; }
  public int MessageSize { get; set; }

}

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

public static class OpenTelemetryExtensions
{
  /// <summary>
  /// https://opentelemetry.io/docs/reference/specification/resource/semantic_conventions/k8s/
  /// </summary>
  public const string K8S_NODE_NAME = "k8s.node.name";
  public const string K8S_POD_NAME = "k8s.pod.name";
  public const string K8S_DEPLOYMENT_NAME = "k8s.deployment.name";
  public static IServiceCollection AddOpenTelemetry(this IServiceCollection serviceCollection, IConfiguration configuration, string serviceName,
    Action<TracerProviderBuilder>? configureTracerProvider = null, Action<MeterProviderBuilder>? configureMetricsProvider = null)
  {
    var openTelemetryConfiguration = configuration.Get<OpenTelemetryConfiguration>();

    if (openTelemetryConfiguration is null)
    {
      return serviceCollection;
    }

    serviceCollection.Configure<OpenTelemetryConfiguration>(configuration);
    serviceCollection.AddSingleton<OpenTelemetryConfigurationMonitor>();
    //serviceCollection.AddSingleton<ActivityFilterProcessor>();
    //serviceCollection.AddSingleton<OpenTelemetrySampler>();
    //serviceCollection.AddSingleton<ActivityKafkaProcessor>();

    //serviceCollection.AddOpenTelemetry()
    //  .WithTracing(builder =>
    //  {
    //    var traceProviderBuilder = BuildDefault(builder, serviceName, openTelemetryConfiguration.HostName);
    //    traceProviderBuilder.AddOtlpExporter(q => q.Endpoint = openTelemetryConfiguration.ExporterEndpoint ?? q.Endpoint);
    //    configureTracerProvider?.Invoke(traceProviderBuilder);

    //  }).WithMetrics(builder =>
    //  {
    //    if (!openTelemetryConfiguration.EnableMetrics) return;

    //    var meterProviderBuilder = BuildDefault(builder, serviceName, openTelemetryConfiguration.HostName);

    //    meterProviderBuilder.AddOtlpExporter(q => q.Endpoint = openTelemetryConfiguration.ExporterEndpoint);
    //    meterProviderBuilder.AddPrometheusExporter();
    //    configureMetricsProvider?.Invoke(meterProviderBuilder);
    //  });

    // It is necessary to activate the logger level "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware", LogLevel.Information
    //serviceCollection.AddHttpLogging(o => o.LoggingFields = HttpLoggingFields.ResponseBody | HttpLoggingFields.RequestBody);
    serviceCollection.AddLogging(logging =>
    {

      logging.AddOpenTelemetry(options =>
      {
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;

        var resBuilder = ResourceBuilder.CreateDefault();
        resBuilder.AddService(serviceName);
        options.SetResourceBuilder(resBuilder);
        //options.AddProcessor(new ActivityEventLogProcessor());
        options.AddOtlpExporter(p => p.Endpoint = openTelemetryConfiguration.ExporterEndpoint);
      });
    });
    return serviceCollection;
  }

  //private static TracerProviderBuilder BuildDefault(TracerProviderBuilder tracerProviderBuilder, string serviceName, string? clusterName, IEnumerable<KeyValuePair<string, object>>? options = null) =>
  //  tracerProviderBuilder
  //    .SetResourceBuilder(ResourceBuilder.CreateDefault()
  //      .AddDetector(new K8SDetector(clusterName))
  //      .AddAttributes(options!)
  //      .AddService(string.IsNullOrWhiteSpace(clusterName) ? $"{clusterName}/{serviceName}" : serviceName, clusterName))
  //    .SetSampler(sp => new ParentBasedSampler(new OpenTelemetrySampler(sp.GetRequiredService<OpenTelemetryConfigurationMonitor>(), sp.GetRequiredService<ILogger<OpenTelemetrySampler>>())))
  //    .AddProcessor<ActivityFilterProcessor>()
  //    .AddAspNetCoreInstrumentation(x =>
  //    {
  //      x.RecordException = true;
  //      x.EnrichWithHttpRequest = (activity, httpRequest) =>
  //      {
  //        if (httpRequest.Headers.Any())
  //          activity.AddEvsHeaders(httpRequest.Headers);
  //      };
  //      x.EnrichWithHttpResponse = (activity, response) =>
  //      {
  //        if (response.StatusCode is >= 200 and <= 299)
  //        {
  //          activity.SetTag("http.response.body", null);
  //          activity.SetStatus(ActivityStatusCode.Ok);
  //          return;
  //        }

  //        activity.SetStatus(ActivityStatusCode.Error, ReasonPhrases.GetReasonPhrase(response.StatusCode));
  //      };
  //    })
  //    .AddHttpClientInstrumentation(x =>
  //    {
  //      x.RecordException = true;
  //      x.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
  //      {
  //        if (httpRequestMessage.Headers.Any())
  //          activity.AddEvsHeaders(httpRequestMessage.Headers);
  //      };
  //    })
  //    .AddSource(OpenTelemetrySources.BusEventService)
  //    .AddSource(OpenTelemetrySources.BusCommandService)
  //    .AddSource(OpenTelemetrySources.KafkaProducer)
  //    .AddSource(OpenTelemetrySources.KafkaConsumer)
  //    .AddSource(OpenTelemetrySources.SagaDispatcher)
  //    .AddSource(OpenTelemetrySources.Npgsql)
  //    .AddProcessor<ActivityKafkaProcessor>();

  //private static MeterProviderBuilder BuildDefault(MeterProviderBuilder meterProviderBuilder, string serviceName, string? clusterName)
  //  => meterProviderBuilder
  //    .SetResourceBuilder(ResourceBuilder.CreateDefault()
  //      .AddService(serviceName, clusterName)
  //      .AddDetector(new K8SDetector(clusterName))
  //      .AddAttributes(KubernetesTags.GetKubernetesTags(clusterName)))

  //    .AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel")
  //    .SetExemplarFilter(new AlwaysOnExemplarFilter())
  //    .AddRuntimeInstrumentation()
  //    .AddProcessInstrumentation()
  //    .AddAspNetCoreInstrumentation((x) => x.Filter = AspnetCoreOtelFilter.Filter)
  //    .AddHttpClientInstrumentation();

  //public static IApplicationBuilder UseOpenTelemetry(this IApplicationBuilder app)
  //{
  //  var openTelemetryConfiguration = app.ApplicationServices.GetService<IOptionsMonitor<OpenTelemetryConfiguration>>();
  //  return openTelemetryConfiguration?.CurrentValue.EnableMetrics == true ? app.UseOpenTelemetryPrometheusScrapingEndpoint().UseHttpLogging() : app.UseHttpLogging();
  //}

}