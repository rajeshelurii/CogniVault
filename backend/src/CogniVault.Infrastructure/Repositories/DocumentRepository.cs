using System;
using System.Collections.Generic;
using System.Text;
using CogniVault.Application.Documents.Interfaces;
using CogniVault.Domain.Entities;

namespace CogniVault.Infrastructure.Repositories
{
    public class DocumentRepository : IDocumentRepository 
    {
        public Task SaveAsync(Document document)
        {
            return Task.CompletedTask;
        }
    }
}
