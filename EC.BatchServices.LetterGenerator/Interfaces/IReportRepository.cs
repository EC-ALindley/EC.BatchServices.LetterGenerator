namespace EC.BatchServices.LetterGenerator.Interfaces
{
    public interface IReportRepository
    {
        Task<IEnumerable<(int ReportQueueID, string ReportName, int ReportTypeID, 
            int DocumentTypeID, int ClaimID, string DocumentFormat)>> GetPendingReportsAsync();
        Task<bool> IsWorkPendingAsync();
        Task<int> UpdateReportDetailsFlags(int queueId);
        Task<List<DTOs.Parameter>> GetReportQueueParametersFromDbAsync(int reportQueueId);
    }

}
