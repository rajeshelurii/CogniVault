using CogniVault.Api.Models.Documents;
using CogniVault.Application.Documents.Commands;
using Microsoft.AspNetCore.Mvc;

namespace CogniVault.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly UploadDocumentCommandHandler _handler;
        public DocumentsController(UploadDocumentCommandHandler handler)
        {
            _handler = handler;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new 
            { 
                Message = "CogniVault API is running!" 
            });
        }

        [HttpPost]
        public IActionResult Upload(UploadDocumentRequest request)
        {
            var command = new UploadDocumentCommand
            {
                FileName = request.File.FileName,
                FileStream = request.File.OpenReadStream()
            };

            _handler.Handle(command);

            return Ok(new
            {
                Message = "Command created successfully!",
                FileName = command.FileName
            });
        }
    }
}
