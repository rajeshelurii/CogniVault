using System;
using System.Collections.Generic;
using System.Text;
using CogniVault.Domain.Entities;

namespace CogniVault.Application.Documents.Interfaces
{
    public interface IDocumentRepository
    {
        public Task SaveAsync(Document document);
    }
}
