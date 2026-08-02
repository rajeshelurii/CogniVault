using System;
using System.Collections.Generic;
using System.Text;

namespace CogniVault.Domain.Entities
{
    public class Document
    {
        public Guid Id { get; private set; }
        public string FileName { get; private set; } = string.Empty;
        public DateTime UploadedAtUtc { get; private set; }
        public Document(string fileName)
        {
            Id = Guid.NewGuid();
            FileName = fileName;
            UploadedAtUtc = DateTime.UtcNow;
        }
    }
}
