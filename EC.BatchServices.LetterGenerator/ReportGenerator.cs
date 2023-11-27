using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Dapper;
using EC.BatchServices.LetterGenerator.DTOs;
using EC.BatchServices.LetterGenerator;
using System.Data;
using System.Net.Http.Headers;
using System.Web;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net.Mime;
using System.Text;

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
        try
        {
            _logger.LogInformation("Retrieving reports pending...");
            var reportsPending = await GetPendingReportsAsync();
            _logger.LogInformation($"{reportsPending.Count()} report(s) are pending.");

            _logger.LogInformation($"Contacting SSRS to generate reports...");
            var reportsGenerated = 0;
            foreach (var request in reportsPending)
            {
                try
                {
                    await GenerateAndSaveReportAsync(request);
                    reportsGenerated++;
                    _logger.LogInformation($"{reportsGenerated} reports have been generated.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error generating report for request on claimID {request.ClaimID}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"An error occurred: {ex.Message}");
        }
    }


    private async Task<List<ReportRequest>> GetPendingReportsAsync()
    {
        var reportsNeededQuery = "SELECT ReportQueueID, ReportName, " +
            "ReportTypeID, DocumentTypeID, ClaimID, DocumentFormat FROM ReportService.ReportQueue WHERE Flag = 1";
        var result = await _dbConnection.QueryAsync<(int ReportQueueID, string ReportName, int ReportTypeID, 
            int DocumentTypeID, int ClaimID, string DocumentFormat)>(reportsNeededQuery);
        return await BuildReportRequestsAsync(result);
    }

    private async Task<List<ReportRequest>> BuildReportRequestsAsync(IEnumerable<(int ReportQueueID, string ReportName, 
        int ReportTypeID, int DocumentTypeID, int ClaimID, string DocumentFormat)> reportsNeeded)
    {
        var reportRequests = new List<ReportRequest>();
        foreach (var report in reportsNeeded)
        {
            var parameters = await GetReportQueueParametersFromDbAsync(report.ReportQueueID);
            reportRequests.Add(new ReportRequest
            {
                ReportQueueID = report.ReportQueueID,
                ReportName = report.ReportName,
                ReportTypeID = report.ReportTypeID,
                DocumentTypeID = report.DocumentTypeID,
                ClaimID = report.ClaimID,
                DocumentFormat = report.DocumentFormat,
                Parameters = parameters
            });
        }
        return reportRequests;
    }

    private async Task GenerateAndSaveReportAsync(ReportRequest reportRequest)
    {
        var client = _httpClientFactory.CreateClient("SSRSClient");

        var reportUrl = await BuildReportUrl(reportRequest);
        var request = new HttpRequestMessage(HttpMethod.Get, reportUrl);
        var response = await client.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsByteArrayAsync();

            var tempFileName = $"{reportRequest.ReportName}_{reportRequest.ClaimID}_{DateTime.UtcNow}";

            await UploadReportDocument(reportRequest, tempFileName, 43, content);

            //Report Type 2 is a RevSpring letter. Add document to the process queue.
            if(reportRequest.ReportTypeID == 2)
            {
                //Add the uploaded document to the EDICloud Queue
                _logger.LogError("Adding the letter to the Processing Queue...");
            }
        }
    }

    private async Task<string> BuildReportUrl(ReportRequest reportRequest)
    {
        var reportParameters = HttpUtility.ParseQueryString(string.Empty);

        foreach (var param in reportRequest.Parameters)
        {
            reportParameters[param.Name] = param.Value;
        }

        // Use the report Path in the URL, and ensure it's URL-encoded (ReportName needs to be updated to ReportPath)
        var encodedReportPath = HttpUtility.UrlEncode(reportRequest.ReportName);

        // Add the format parameter for PDF
        reportParameters["rs:Format"] = "PDF";

        var parametersString = reportParameters.ToString();
        var reportUrl = $"http://ecdev02/ReportServer?{encodedReportPath}&{parametersString}";

        _logger.LogInformation($"Sending Report Generation request, URL: {reportUrl}");
        return reportUrl;
    }

    public async Task<IEnumerable<ReportDetailDTO>> FetchReportDetailsAsync()
    {
        var client = _httpClientFactory.CreateClient("SSRSClient");
        var response = await client.GetAsync(_config.SSRS.BaseAddress + "/Reports/api/v2.0/CatalogItems");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var jsonObject = JObject.Parse(content);

        // Extract the array associated with the 'value' key
        var itemsArray = jsonObject["value"].ToString();
        var reportDetails = JsonConvert.DeserializeObject<IEnumerable<ReportDetailDTO>>(itemsArray);

        return reportDetails;
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
        var query = "UPDATE EnforcerServices.ReportService.ReportQueue SET Flag = 0 WHERE ReportQueueID = @QueueId";

        var result = await _dbConnection.ExecuteAsync(query, new { QueueId = queueId });

        if (result == 0)
        {
            _logger.LogError("No report details were updated, check if the queueId is correct.");
        }
    }
    private async Task UploadReportDocument(ReportRequest request, string fileName, int userID, byte[] image)
    {
        var client = _httpClientFactory.CreateClient("DocumentImagingClient");
        var requestContent = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(image);
        if(request.DocumentFormat == "pdf")
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf"); // Set the correct content type

        requestContent.Add(imageContent, "Image", fileName);

        var document = new EC.BatchServices.LetterGenerator.DAOs.Document
        {
            ClaimID = request.ClaimID,
            DocumentTypeID = request.DocumentTypeID,
            DocumentRequestID = null,
            FileID = 0,
            FileGuid = Guid.NewGuid(),
            ChangeTime = DateTime.Now,
            CreationTime = DateTime.Now,
            Title = "SystemLetter",
            Extension = request.DocumentFormat,
            FileName = fileName,
            FileSize = image.Length,
            LastAccessTime = DateTime.Now,
            LastWriteTime = DateTime.Now,
            UserID = userID,
            Image = null
        };
        // Serialize your document object to JSON and add it to the request
        var documentJson = JsonConvert.SerializeObject(document);
        requestContent.Add(new StringContent(documentJson, Encoding.UTF8, "application/json"), "Document");

        _logger.LogInformation($"Uploading document to DocumentImaging Service...");
        var response = await client.PostAsync("http://ecdev04/DocumentImaging/api/Document/InsertDocument", requestContent);
        if (!response.IsSuccessStatusCode)
        {
            string responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Error: {response.StatusCode}, {responseContent}");

            // Throwing an exception with detailed information
            throw new HttpRequestException($"Request to DocumentImaging Service failed with status code {response.StatusCode}: {responseContent}");
        }
        else
        {
            _logger.LogInformation($"Upload Successful! ", response.Content);
        }
    }

    //TODO: Create Note about Letter being sent
}
