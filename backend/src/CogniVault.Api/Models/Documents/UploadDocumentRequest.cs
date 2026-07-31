namespace CogniVault.Api.Models.Documents
{
    public class UploadDocumentRequest
    {
        public IFormFile File { get; set; } = default!;
    }
}
