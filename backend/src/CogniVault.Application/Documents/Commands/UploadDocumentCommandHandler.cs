using System;
using System.Collections.Generic;
using System.Text;
using CogniVault.Application.Documents.Interfaces;
using CogniVault.Domain.Entities;

namespace CogniVault.Application.Documents.Commands
{
    public class UploadDocumentCommandHandler
    {
        private readonly IDocumentRepository _repository;

        public UploadDocumentCommandHandler(IDocumentRepository repository)
        {
            _repository = repository;
        }
        public async Task Handle(UploadDocumentCommand command)
        {
            Console.WriteLine($"Uploading: {command.FileName}");

            var document = new Document(command.FileName, "someId.pdf", "application/pdf", 100);
            await _repository.SaveAsync(document);

        }
    }
}
