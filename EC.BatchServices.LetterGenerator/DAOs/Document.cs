namespace EC.BatchServices.LetterGenerator.DAOs
{
    public class Document
    {
        public int? DocumentID { get; set; }
        public int? ClaimID { get; set; }
        public int? DocumentRequestID { get; set; }
        public int? DocumentTypeID { get; set; }
        public int? FileID { get; set; }
        public Guid FileGuid { get; set; }
        public DateTime? ChangeTime { get; set; }
        public DateTime? CreationTime { get; set; }
        public string Title { get; set; }
        public string Extension { get; set; }
        public string FileName { get; set; }
        public int? FileSize { get; set; }
        public DateTime? LastAccessTime { get; set; }
        public DateTime? LastWriteTime { get; set; }
        public int? UserID { get; set; }
        public string Version { get; set; }
        public byte[] Image { get; set; }
    }
}
