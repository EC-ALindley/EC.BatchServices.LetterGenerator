using System.ComponentModel.DataAnnotations;

namespace EC.BatchServices.LetterGenerator.DTOs
{
    public class DocumentDto
    {
        public int? DocumentID { get; set; }

        [Required]
        public int? ClaimID { get; set; }

        public int? DocumentRequestID { get; set; }

        [Required]
        public int? DocumentTypeID { get; set; }

        public int? FileID { get; set; }

        [Required]
        public Guid FileGuid { get; set; }

        public DateTime? ChangeTime { get; set; }

        [Required]
        public DateTime? CreationTime { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]
        [StringLength(10)]
        public string Extension { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; }

        public int? FileSize { get; set; }

        public DateTime? LastAccessTime { get; set; }

        public DateTime? LastWriteTime { get; set; }

        [Required]
        public int? UserID { get; set; }

        [StringLength(50)]
        public string Version { get; set; }

        public byte[] Image { get; set; }
    }

}
