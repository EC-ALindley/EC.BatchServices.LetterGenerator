namespace EC.BatchServices.LetterGenerator.DTOs
{
    public class ReportRequest
    {
        public int ReportQueueId { get; set; }
        public string ReportName { get; set; }
        public List<Parameter> Parameters { get; set; }
    }
}
