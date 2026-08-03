using System;
using System.Collections.Generic;
using System.Text;

namespace CogniVault.Domain.Entities
{
    public class Document
    {
        public Guid Id { get; private set; }
        public string FileName { get; private set; } = default!;
        public string StoredFileName { get; private set; } = default!;
        public string ContentType { get; private set; } = default!;
        public long FileSize { get; private set; }
        public DateTime UploadedAtUtc { get; private set; }

        private Document() { } // For EF Core

        public Document(string fileName, string storedFileName, string contentType, long fileSize)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name can not be empty", nameof(fileName));

            if (string.IsNullOrEmpty(storedFileName))
                throw new ArgumentException("Stored file name can not be empty", nameof(storedFileName));

            if (string.IsNullOrEmpty(contentType))
                throw new ArgumentException("Content type can not be empty", nameof(contentType));

            if (fileSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(fileSize));

            Id = Guid.NewGuid();
            FileName = fileName;
            StoredFileName = storedFileName;
            ContentType = contentType;
            FileSize = fileSize;
            UploadedAtUtc = DateTime.UtcNow;
        }
    }
}
