using CogniVault.Application.Documents.Commands;
using CogniVault.Application.Documents.Interfaces;
using CogniVault.Infrastructure.Repositories;
using CogniVault.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Adding the class to the DI container
builder.Services.AddScoped<UploadDocumentCommandHandler>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();