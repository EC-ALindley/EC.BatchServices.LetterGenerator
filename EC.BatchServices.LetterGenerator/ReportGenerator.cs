using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Dapper;
using System.Data;
using EC.BatchServices.LetterGenerator.DTOs;
using EC.BatchServices.LetterGenerator.DAOs;
using System.Web;
using System.Reflection.Metadata;
using EC.BatchServices.LetterGenerator;
using System.Net.Http.Headers;

public class ReportGenerator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Config _config;
    private readonly IDbConnection _dbConnection;
    private readonly ILogger<ReportGenerator> _logger;

    public ReportGenerator(
        IHttpClientFactory httpClientFactory,
        IOptions<Config> config,
        IDbConnection dbConnection,
        ILogger<ReportGenerator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
        _dbConnection = dbConnection;
        _logger = logger;
    }

    public async Task GenerateAndSaveReportsAsync()
    {
        _logger.LogInformation("Retrieving reports pending...");
        var reportsNeeded = await GetPendingReportsAsync();
        _logger.LogInformation($"{reportsNeeded.Count()} report(s) are pending.");

        _logger.LogInformation($"Building report requests...");
        var reportRequests = await BuildReportRequestsAsync(reportsNeeded);

        foreach (var reportRequest in reportRequests)
        {
            await GenerateAndSaveReportAsync(reportRequest);
        }
    }

    private async Task<IEnumerable<(int ReportQueueId, string ReportName)>> GetPendingReportsAsync()
    {
        var reportsNeededQuery = "SELECT ReportQueueId, ReportName FROM ReportService.ReportQueue WHERE Flag = 1";
        return await _dbConnection.QueryAsync<(int ReportQueueId, string ReportName)>(reportsNeededQuery);
    }

    private async Task<List<ReportRequest>> BuildReportRequestsAsync(IEnumerable<(int ReportQueueId, string ReportName)> reportsNeeded)
    {
        var reportRequests = new List<ReportRequest>();
        foreach (var report in reportsNeeded)
        {
            var parameters = await GetReportQueueParametersFromDbAsync(report.ReportQueueId);
            reportRequests.Add(new ReportRequest
            {
                ReportQueueId = report.ReportQueueId,
                ReportName = report.ReportName,
                Parameters = parameters
            });
        }
        return reportRequests;
    }

    private async Task GenerateAndSaveReportAsync(ReportRequest reportRequest)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_config.SSRS.Username}:{_config.SSRS.Password}")));

        var reportUrl = BuildReportUrl(reportRequest);
        var reportData = await GetReportDataAsync(client, reportUrl);
        // Add logic to save the report data
    }

    private string BuildReportUrl(ReportRequest reportRequest)
    {
        var reportParameters = HttpUtility.ParseQueryString(string.Empty);
        foreach (var param in reportRequest.Parameters)
        {
            reportParameters[param.Name] = param.Value;
        }
        return $"{_config.SSRS.BaseAddress}/Reports/{reportRequest.ReportName}/ExportToPDF?{reportParameters}";
    }

    private async Task<byte[]> GetReportDataAsync(HttpClient client, string reportUrl)
    {
        var reportResponse = await client.GetAsync(reportUrl);
        reportResponse.EnsureSuccessStatusCode();
        return await reportResponse.Content.ReadAsByteArrayAsync();
    }


    public async Task<bool> IsWorkPendingAsync()
    {
        var workCountQuery = "SELECT COUNT(*) FROM ReportService.ReportQueue WHERE Flag = 1";
        var workCount = await _dbConnection.ExecuteScalarAsync<int>(workCountQuery);

        return workCount > 0;
    }
    private async Task<List<EC.BatchServices.LetterGenerator.DTOs.Parameter>> GetReportQueueParametersFromDbAsync(int reportQueueId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@ReportQueueId", reportQueueId, DbType.Int32);

        var result = await _dbConnection.QueryAsync<EC.BatchServices.LetterGenerator.DTOs.Parameter>("ReportService.GetReportParameters", parameters, commandType: CommandType.StoredProcedure);
        return result.AsList();
    }
    private async Task UpdateReportDetailsFlags(int queueId)
    {
        var query = "UPDATE EnforcerServices.BatchServices.ReportsQueue SET Flag = 0 WHERE ReportsQueueId = @QueueId";

        var result = await _dbConnection.ExecuteAsync(query, new { QueueId = queueId });

        if (result == 0)
        {
            throw new Exception("No report details were updated, check if the queueId is correct.");
        }
    }
    public async Task UploadReportDocument(int claimID, int? documentRequestID, int documentTypeID, string fileName, string title,
        string extension, int userID, byte[] image)
    {
        var url = _config.DocumentImaging.BaseAddress; // Replace with your configuration access method

        var client = _httpClientFactory.CreateClient(); // Using HttpClientFactory

        var document = new EC.BatchServices.LetterGenerator.DAOs.Document
        {
            ClaimID = claimID,
            DocumentTypeID = documentTypeID,
            DocumentRequestID = documentRequestID,
            FileID = 0,
            FileGuid = Guid.NewGuid(),
            ChangeTime = DateTime.Now,
            CreationTime = DateTime.Now,
            Title = title,
            Extension = extension,
            FileName = fileName,
            FileSize = image.Length,
            LastAccessTime = DateTime.Now,
            LastWriteTime = DateTime.Now,
            UserID = userID,
            Image = null
        };

        var requestContent = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(image);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg"); // Adjust the MIME type as necessary
        requestContent.Add(imageContent, "Image", fileName);
        requestContent.Add(new StringContent(JsonConvert.SerializeObject(document)), "Document");

        var response = await client.PostAsync($"{url}/api/Document/InsertDocument", requestContent);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
    }

}
