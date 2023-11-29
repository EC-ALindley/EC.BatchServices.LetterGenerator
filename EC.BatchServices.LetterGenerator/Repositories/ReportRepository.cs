using Dapper;
using EC.BatchServices.LetterGenerator.Interfaces;
using System.Data;

namespace EC.BatchServices.LetterGenerator.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly IDbConnection _dbConnection;

        public ReportRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<(int ReportQueueID, string ReportName, int ReportTypeID, int DocumentTypeID, int ClaimID, string DocumentFormat)>> GetPendingReportsAsync()
        {
            var query = "SELECT ReportQueueID, ReportName, ReportTypeID, DocumentTypeID, ClaimID, DocumentFormat FROM ReportService.ReportQueue WHERE Flag = 1";
            
            return await _dbConnection.QueryAsync<(int, string, int, int, int, string)>(query);
        }
        public async Task<bool> IsWorkPendingAsync()
        {
            var workCountQuery = "SELECT COUNT(*) FROM ReportService.ReportQueue WHERE Flag = 1";
            var workCount = await _dbConnection.ExecuteScalarAsync<int>(workCountQuery);

            return workCount > 0;
        }
        public async Task<int> UpdateReportDetailsFlags(int queueId)
        {
            var query = "UPDATE EnforcerServices.ReportService.ReportQueue SET Flag = 0 WHERE ReportQueueID = @QueueId";

            return await _dbConnection.ExecuteAsync(query, new { QueueId = queueId });
        }
        public async Task<List<DTOs.Parameter>> GetReportQueueParametersFromDbAsync(int reportQueueId)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@ReportQueueId", reportQueueId, DbType.Int32);

            var result = await _dbConnection.QueryAsync<DTOs.Parameter>("ReportService.GetReportParameters", parameters, commandType: CommandType.StoredProcedure);
            return result.AsList();
        }
    }
}
