using CogniVault.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace CogniVault.Application.Documents.Commands
{
    public class UploadDocumentCommandHandler
    {
        private readonly IDocumentRepository _repository;

        public UploadDocumentCommandHandler(IDocumentRepository repository)
        {
            _repository = repository;
        }
        public void Handle(UploadDocumentCommand command)
        {
            Console.WriteLine($"Uploading: {command.FileName}");
        }
    }
}
