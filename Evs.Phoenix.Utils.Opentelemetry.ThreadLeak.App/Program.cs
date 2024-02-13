using Microsoft.AspNetCore;

namespace Evs.Phoenix.Utils.OpenTelemetry.Test.App;

public class Program
{
  public static async Task Main(string[] args)
  {
    const int port = 1819;

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
        
        //let's simulate a refresh of the configuration
        configRoot.Reload();

        await Task.Delay(250);
      }
    });

    await host.RunAsync().ConfigureAwait(false);
  }
}