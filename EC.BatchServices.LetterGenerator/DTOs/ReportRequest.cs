namespace EC.BatchServices.LetterGenerator.DTOs
{
    public class ReportRequest
    {
        public int ReportQueueID { get; set; }
        public string ReportName { get; set; }
        public int ReportTypeID { get; set; }
        public int DocumentTypeID { get; set; }
        public int ClaimID { get; set; }
        public string DocumentFormat {  get; set; }
        public List<Parameter> Parameters { get; set; }
    }
}
