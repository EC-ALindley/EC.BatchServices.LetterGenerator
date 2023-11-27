using NLog.Extensions.Logging;
using System.Data;
using System.Data.SqlClient;
using System.Net.Http.Headers;

namespace EC.BatchServices.LetterGenerator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureLogging((context, builder) =>
                {
                    builder.ClearProviders();
                    builder.AddNLog(context.Configuration);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var configuration = hostContext.Configuration;

                    services.AddHttpClient();
                    services.AddTransient<ReportGenerator>();
                    services.AddHostedService<Worker>();

                    services.Configure<Config>(config =>
                    {
                        config.DocumentImaging = configuration.GetSection("DocumentImagingConfig").Get<DocumentImagingConfig>();
                        config.SSRS = configuration.GetSection("SSRSConfig").Get<SSRSConfig>();
                    });

                    services.AddTransient<IDbConnection>(sp =>
                        new SqlConnection(configuration.GetConnectionString("EnforcerServices")));

                    services.AddHttpClient("SSRSClient", client =>
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Negotiate");
                        client.Timeout = Timeout.InfiniteTimeSpan;
                    })
                    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                    {
                        UseDefaultCredentials = true
                    });
                    services.AddHttpClient("DocumentImagingClient", client =>
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Negotiate");
                    })
                    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                    {
                        UseDefaultCredentials = true
                    });
                })
                .Build();

            await host.RunAsync();
        }
    }
}