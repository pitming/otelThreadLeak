using System.Diagnostics;

namespace Evs.Phoenix.Utils.OpenTelemetry.Test.App;

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
