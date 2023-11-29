namespace EC.BatchServices.LetterGenerator
{
    public class DocumentType
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string MIMEType { get; set; }

        public DocumentType(int id, string name, string mimeType)
        {
            ID = id;
            Name = name;
            MIMEType = mimeType;
        }
    }

    public class DocumentTypeRegistry
    {
        public static List<DocumentType> GetDocumentTypes()
        {
            return new List<DocumentType>
        {
            new DocumentType(1, "pdf", "application/pdf"),
            new DocumentType(2, "word", "application/msword"),
            new DocumentType(3, "wordx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            new DocumentType(4, "excel", "application/vnd.ms-excel"),
            new DocumentType(5, "excelx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            // ... Add other document types here
        };
        }
    }

}
