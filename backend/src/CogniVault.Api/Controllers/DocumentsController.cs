using Microsoft.AspNetCore.Mvc;

namespace CogniVault.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new 
            { 
                Message = "CogniVault API is running!" 
            });
        }
    }
}
