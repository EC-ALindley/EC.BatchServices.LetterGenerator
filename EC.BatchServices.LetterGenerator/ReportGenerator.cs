using Newtonsoft.Json;
using Dapper;
using EC.BatchServices.LetterGenerator.DTOs;
using EC.BatchServices.LetterGenerator;
using System.Data;
using System.Net.Http.Headers;
using System.Web;
using RestSharp;
using System.Text;
using EC.BatchServices.LetterGenerator.Interfaces;

public class ReportGenerator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDbConnection _dbConnection;
    private readonly ILoggerAdapter<ReportGenerator> _logger;
    private readonly IReportRepository _reportRepository;

    public ReportGenerator(
        IHttpClientFactory httpClientFactory,
        IDbConnection dbConnection,
        ILoggerAdapter<ReportGenerator> logger,
        IReportRepository reportRepository)
    {
        _httpClientFactory = httpClientFactory;
        _dbConnection = dbConnection;
        _logger = logger;
        _reportRepository = reportRepository;
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
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"Error generating report for request on claimID {request.ClaimID}: {ex.Message}");
                }
            }
            _logger.LogInformation($"{reportsGenerated} reports have been generated.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"An error occurred: {ex}");
        }
    }
    protected async Task<List<ReportRequest>> GetPendingReportsAsync()
    {
        var reportsNeeded = await _reportRepository.GetPendingReportsAsync();
        return await BuildReportRequestsAsync(reportsNeeded);
    }
    public async Task<List<ReportRequest>> BuildReportRequestsAsync(IEnumerable<(int ReportQueueID, string ReportName, 
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
                _logger.LogInformation("Adding the letter to the Processing Queue...");
            }

            _logger.LogInformation("Removing flag from queue item...");
            await UpdateReportDetailsFlags(reportRequest.ReportQueueID);
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

    public async Task<bool> IsWorkPendingAsync()
    {
        return await _reportRepository.IsWorkPendingAsync();
    }
    public async Task<List<EC.BatchServices.LetterGenerator.DTOs.Parameter>> GetReportQueueParametersFromDbAsync(int reportQueueId)
    {
        return await _reportRepository.GetReportQueueParametersFromDbAsync(reportQueueId);
    }
    public async Task UpdateReportDetailsFlags(int queueId)
    {
        var result = await _reportRepository.UpdateReportDetailsFlags(queueId);

        if (result == 0)
        {
            _logger.LogInformation("No report details were updated, check if the queueId is correct.");
        }

        _logger.LogInformation($"Item number {queueId} removed from the report queue.");
    }
    public async Task UploadReportDocument(ReportRequest request, string fileName, int userID, byte[] image)
    {
        var client = _httpClientFactory.CreateClient("DocumentImagingClient");
        var requestContent = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(image);
        List<DocumentType> docTypes = DocumentTypeRegistry.GetDocumentTypes();


        // Find the DocumentType based on the request's DocumentFormat
        var documentType = docTypes.FirstOrDefault(dt => dt.Name.Equals(request.DocumentFormat, 
            StringComparison.OrdinalIgnoreCase));

        if (documentType != null)
        {
            // Set the correct content type based on the document type
            imageContent.Headers.ContentType = new MediaTypeHeaderValue(documentType.MIMEType);
        }
        else
        {
            _logger.LogError($"Error: Document Type {request.DocumentFormat} is not a valid format.");
            return;
        }

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
            //This Title may need to be passed in
            Title = "System Letter",
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
            _logger.LogInformation($"Upload Successful!");
        }
    }

    //TODO: Create Note about Letter being sent
}
