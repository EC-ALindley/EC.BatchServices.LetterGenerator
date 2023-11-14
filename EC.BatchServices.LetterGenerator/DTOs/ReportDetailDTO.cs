namespace EC.BatchServices.LetterGenerator.DTOs
{
    public class ReportDetailDTO 
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public bool Hidden { get; set; }
        public int Size { get; set; }
        public string ModifiedBy { get; set; }
        public string ModifiedDate { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedDate { get; set; }
        public Guid ParentFolderId { get; set; }
        public string ContentType { get; set; }
        public string Content { get; set; }
        public bool IsFavorite { get; set; }
        public bool HasDataSources { get; set; }
        public bool HasSharedDataSets { get; set; }
        public bool HasParameters { get; set; }
        public List<Parameter>? Parameters { get; set; }
    }
}
