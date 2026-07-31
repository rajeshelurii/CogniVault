using System;
using System.Collections.Generic;
using System.Text;

namespace CogniVault.Application.Documents.Commands
{
    public class UploadDocumentCommand
    {
        public string FileName { get; set; } = string.Empty;
        public Stream FileStream { get; init; } = Stream.Null;
    }
}
