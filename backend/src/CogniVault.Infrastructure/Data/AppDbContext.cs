using Microsoft.EntityFrameworkCore;
using CogniVault.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CogniVault.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Document> Documents { get; set; }
    }
}
