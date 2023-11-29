using Dapper;
using EC.BatchServices.LetterGenerator.DTOs;
using EC.BatchServices.LetterGenerator.Interfaces;
using Moq;
using Moq.Protected;
using System.Data;
using System.Net;

namespace EC.BatchServices.LetterGenerator.Tests
{
    public class ReportGeneratorTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IDbConnection> _dbConnectionMock;
        private readonly Mock<ILoggerAdapter<ReportGenerator>> _loggerMock;
        private readonly Mock<IReportRepository> _reportRepository;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
                                                              
        private readonly ReportGenerator _reportGenerator;

        public ReportGeneratorTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _dbConnectionMock = new Mock<IDbConnection>();
            _loggerMock = new Mock<ILoggerAdapter<ReportGenerator>>();
            _reportRepository = new Mock<IReportRepository>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("http://example.com") // Use the appropriate base address
            };
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);


            _reportGenerator = new ReportGenerator(
                _httpClientFactoryMock.Object,
                _dbConnectionMock.Object,
                _loggerMock.Object,
                _reportRepository.Object);
        }

        #region GenerateAndSaveReportAsync
        [Fact]
        public async Task GenerateAndSaveReportsAsync_ShouldRetrievePendingReportsAndLog()
        {
            // Arrange
            var reportsPending = new List<(int ReportQueueID, string ReportName, int ReportTypeID, int DocumentTypeID, int ClaimID,
                string DocumentFormat)>
            {
                (1, "Report1", 2, 10, 301, "PDF"),
                (2, "Report2", 2, 10, 302, "Excel"),
                (3, "Report3", 2, 10, 303, "Word")
            };
            _reportRepository.Setup(x => x.GetPendingReportsAsync()).ReturnsAsync(reportsPending);

            // Act
            await _reportGenerator.GenerateAndSaveReportsAsync();

            // Assert
            _loggerMock.Verify(x => x.LogInformation(It.IsAny<string>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task GenerateAndSaveReportsAsync_ShouldHandleReportGenerationException()
        {
            // Arrange
            var reportsPending = new List<(int ReportQueueID, string ReportName, int ReportTypeID, int DocumentTypeID, int ClaimID,
                string DocumentFormat)>
            {
                (1, "Report1", 2, 10, 301, "PDF"),
                (2, "Report2", 2, 10, 302, "Excel"),
                (3, "Report3", 2, 10, 303, "Word")
            };

            _reportRepository.Setup(x => x.GetPendingReportsAsync()).ReturnsAsync(reportsPending);

            // Setup the HttpClientFactory to throw an exception for a specific client or for all clients
            _httpClientFactoryMock.Setup(x => x.CreateClient("SSRSClient")).Throws(new HttpRequestException());

            // Act
            await _reportGenerator.GenerateAndSaveReportsAsync();

            // Assert
            _loggerMock.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.AtLeastOnce);

            // Optional: Additional assertions can be made here to check the number of times logging occurred, 
            // or the specific message content if that's relevant to your test.
        }

        [Fact]
        public async Task GenerateAndSaveReportsAsync_ShouldHandleGeneralException()
        {
            // Arrange
            var mockReports = new List<(int ReportQueueID, string ReportName, int ReportTypeID, int DocumentTypeID, int ClaimID,
                string DocumentFormat)>
            {
                (1, "Report1", 2, 10, 301, "PDF"),
                (2, "Report2", 2, 10, 302, "Excel"),
                (3, "Report3", 2, 10, 303, "Word")
            };
            _reportRepository.Setup(x => x.GetPendingReportsAsync()).ReturnsAsync(mockReports);
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Throws(new Exception("General error"));

            // Act
            await _reportGenerator.GenerateAndSaveReportsAsync();

            // Assert
            _loggerMock.Verify(x => x.LogError(It.Is<string>(s => s.Contains("An error occurred")), It.IsAny<Exception>()), Times.AtLeastOnce);
        }
        #endregion

        #region IsWorkPendingAsync
        [Fact]
        public async Task IsWorkPendingAsync_ShouldReturnTrueWhenWorkIsPending()
        {
            // Arrange
            _reportRepository.Setup(x => x.IsWorkPendingAsync()).ReturnsAsync(true);

            // Act
            var result = await _reportGenerator.IsWorkPendingAsync();

            // Assert
            Assert.True(result);
        }
        #endregion

        #region UpdateReportDetailsFlags
        [Fact]
        public async Task UpdateReportDetailsFlags_UpdatesSuccessfully_LogsRemoval()
        {
            // Arrange
            int queueId = 1;
            _reportRepository.Setup(x => x.UpdateReportDetailsFlags(queueId)).ReturnsAsync(1);

            // Act
            await _reportGenerator.UpdateReportDetailsFlags(queueId);

            // Assert
            _loggerMock.Verify(x => x.LogInformation($"Item number {queueId} removed from the report queue."), Times.Once);
        }

        [Fact]
        public async Task UpdateReportDetailsFlags_NoUpdate_LogsWarning()
        {
            // Arrange
            int queueId = 1;
            _reportRepository.Setup(x => x.UpdateReportDetailsFlags(queueId)).ReturnsAsync(0);

            // Act
            await _reportGenerator.UpdateReportDetailsFlags(queueId);

            // Assert
            _loggerMock.Verify(x => x.LogInformation("No report details were updated, check if the queueId is correct."), Times.Once);
        }
        #endregion

        #region UploadReportDocument
        [Fact]
        public async Task UploadReportDocument_UploadsSuccessfully_LogsSuccess()
        {
            // Arrange
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(responseMessage);

            var mockReportRequest = new ReportRequest
            {
                ReportQueueID = 123, 
                ReportName = "Sample Report",
                ReportTypeID = 1, 
                DocumentTypeID = 2, 
                ClaimID = 456, 
                DocumentFormat = "pdf",
            };

            var fileName = "test.pdf";
            var userId = 43;
            var image = new byte[] { 0x01, 0x02 };

            // Act
            await _reportGenerator.UploadReportDocument(mockReportRequest, fileName, userId, image);

            // Assert
            _loggerMock.Verify(x => x.LogInformation("Upload Successful!"), Times.Once);
        }

        [Fact]
        public async Task UploadReportDocument_HandlesPdf_CorrectContentType()
        {
            // Arrange
            var requestContentCaptor = new HttpRequestMessage();
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => requestContentCaptor = request)
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var mockReportRequest = new ReportRequest
            {
                ReportQueueID = 123,
                ReportName = "Sample Report",
                ReportTypeID = 1,
                DocumentTypeID = 2,
                ClaimID = 456,
                DocumentFormat = "pdf",
            };

            var fileName = "test.pdf";
            var userId = 43;
            var image = new byte[] { 0x01, 0x02 };

            // Act
            await _reportGenerator.UploadReportDocument(mockReportRequest, fileName, userId, image);

            // Assert
            Assert.NotNull(requestContentCaptor);
            var content = requestContentCaptor.Content as MultipartFormDataContent;
            Assert.NotNull(content);

            // Extract the part of the content that represents the image
            var imageContent = content?.FirstOrDefault(c => c.Headers.ContentDisposition.Name.Trim('"') == "Image");
            Assert.NotNull(imageContent);

            var byteArrayContent = imageContent as ByteArrayContent;
            Assert.NotNull(byteArrayContent);

            var uploadedImage = await byteArrayContent.ReadAsByteArrayAsync();
            Assert.NotNull(uploadedImage);
            Assert.True(uploadedImage.Length > 0); // Ensure that there is data in the byte array

            // Verify content type for PDF
            Assert.Equal("application/pdf", byteArrayContent.Headers.ContentType.MediaType);
        }

        [Fact]
        public async Task UploadReportDocument_UnsuccessfulUpload_LogsErrorAndThrows()
        {
            // Arrange
            var responseMessage = new HttpResponseMessage(HttpStatusCode.BadRequest); // Simulate a bad request response
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(responseMessage);

            var mockReportRequest = new ReportRequest
            {
                ReportQueueID = 123,
                ReportName = "Sample Report",
                ReportTypeID = 1,
                DocumentTypeID = 2,
                ClaimID = 456,
                DocumentFormat = "pdf",
            };

            var fileName = "test.pdf";
            var userId = 43;
            var image = new byte[] { 0x01, 0x02 };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                _reportGenerator.UploadReportDocument(mockReportRequest, fileName, userId, image));

            // Verify that the error log was written
            _loggerMock.Verify(x => x.LogError(
                It.IsAny<string>(),
                It.IsAny<Exception>()),
                Times.Once);

            // Optionally, you can assert parts of the exception message if needed
            Assert.Contains("BadRequest", exception.Message);
        }

        #endregion
    }
}
