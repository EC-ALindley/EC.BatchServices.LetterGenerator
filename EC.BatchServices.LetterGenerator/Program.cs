using EC.BatchServices.LetterGenerator;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Data.SqlClient;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        // Retrieve the configuration from the host context
        var configuration = hostContext.Configuration;

        services.AddHttpClient();
        services.AddTransient<ReportGenerator>();
        services.AddHostedService<Worker>();

        // Configure specific sections for DocumentImaging and SSRS
        services.Configure<Config>(config =>
        {
            config.DocumentImaging = configuration.GetSection("DocumentImagingConfig").Get<DocumentImagingConfig>();
            config.SSRS = configuration.GetSection("SSRSConfig").Get<SSRSConfig>();
        });

        // Use the retrieved configuration to get the connection string
        services.AddTransient<IDbConnection>(sp =>
            new SqlConnection(configuration.GetConnectionString("DefaultConnection")));
    })
    .Build();


await host.RunAsync();
