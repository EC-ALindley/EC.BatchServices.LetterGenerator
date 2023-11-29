using EC.BatchServices.LetterGenerator.Configs;
using EC.BatchServices.LetterGenerator.Interfaces;
using EC.BatchServices.LetterGenerator.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Net.Http.Headers;
using System.Threading.Tasks;

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
                    services.AddTransient<ReportGenerator>(); // Ensure ReportGenerator's dependencies are registered
                    services.AddHostedService<Worker>(); // Ensure Worker's dependencies are registered
                    services.AddScoped(typeof(ILoggerAdapter<>), typeof(NLogAdapter<>));
                    services.AddScoped(typeof(IReportRepository), typeof(ReportRepository));

                    // Ensure Config classes are correctly defined and accessible
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
                        client.Timeout = TimeSpan.FromMinutes(10); // Example timeout
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
