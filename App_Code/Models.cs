using System;

namespace PatientFiles
{
    public sealed class PatientDto
    {
        public int mkabid { get; set; }
        public string fio { get; set; }
        public DateTime? birthDate { get; set; }
    }

    public class FileDto
    {
        public int attachmentId { get; set; }
        public int fileInfoId { get; set; }
        public DateTime? documentDate { get; set; }
        public DateTime? createdAt { get; set; }
        public string fileType { get; set; }
        public string fileTypeCode { get; set; }
        public string description { get; set; }
        public string createUserName { get; set; }
        public string doctorName { get; set; }
        public string fileName { get; set; }
        public string extension { get; set; }
    }

    internal sealed class FileRecord : FileDto
    {
        public int mkabid { get; set; }
        public string path { get; set; }
    }
}
