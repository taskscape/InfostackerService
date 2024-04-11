using System.Net;
using Microsoft.AspNetCore.Mvc;
using ShareAPI.Services;

namespace ShareAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SharingController : ControllerBase
    {
        private readonly ISharingService _sharingService;

        public SharingController(ISharingService sharingService)
        {
            _sharingService = sharingService;
        }
        
        [HttpPost("UploadMarkdownWithFiles")]
        public async Task<IActionResult> UploadMarkdownWithFiles([FromForm] string markdown, [FromForm] List<IFormFile> files)
        {
            Guid identifier = await _sharingService.UploadMarkdownWithFiles(markdown, files);
            return Ok(new { Message = "Markdown and files uploaded successfully.", Guid = identifier });
        }

        [HttpGet("{identifier}")]
        public async Task<IActionResult> GetMarkdownContent(string identifier)
        {
            string? result = await _sharingService.GetMarkdownContent(identifier);
            if (result is null)
            {
                return NotFound(new { Message = "No markdown or files with given identifier found.", Guid = identifier });
            }

            return new ContentResult {
                ContentType = "text/html",
                StatusCode = (int)HttpStatusCode.OK,
                Content = result
            };
        }
        
        [HttpPut("{identifier}")]
        public async Task<IActionResult> UpdateMarkdownWithFiles([FromForm] string markdown, [FromForm] List<IFormFile> files, Guid identifier)
        {
            if (!await _sharingService.UpdateMarkdownWithFiles(markdown, files, identifier))
            {
                return NotFound(new { Message = "No markdown or files with given identifier found.", Guid = identifier });
            }
            
            return Ok(new { Message = "Markdown and files updated successfully.", Guid = identifier });
        }
        
        [HttpDelete("{identifier}")]
        public async Task<IActionResult> DeleteMarkdownWithFiles(string identifier)
        {

            if (!await _sharingService.DeleteMarkdownWithFiles(identifier))
            {
                return NotFound(new { Message = "No markdown or files with given identifier found.", Guid = identifier });
            }

            return Ok(new { Message = "Markdown and files successfully deleted.", Guid = identifier });
        }
        
        [HttpGet("Pdf/{identifier}/{fileName}")]
        public async Task<IActionResult> GetPdf(string identifier, string fileName)
        {
            FileStream stream = await _sharingService.GetPdf(identifier, fileName);
            if (stream is null)
            {
                return NotFound(new { Message = "PDF does not exist.", Guid = identifier });
            }
            return File(stream, "application/pdf");
        }
        
        [HttpGet("Doc/{identifier}/{fileName}")]
        public async Task<IActionResult> GetDoc(string identifier, string fileName)
        {
            FileStream stream = await _sharingService.GetDoc(identifier, fileName);
            if (stream is null)
            {
                return NotFound(new { Message = "Doc does not exist.", Guid = identifier });
            }
            return File(stream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        }
        
        [HttpGet("Image/{identifier}/{fileName}")]
        public async Task<IActionResult> GetImage(string identifier, string fileName)
        {
            FileStream stream = await _sharingService.GetImage(identifier, fileName);
            if (stream is null)
            {
                return NotFound(new { Message = "Image does not exist.", Guid = identifier });
            }
            return File(stream, "image/png");
        }
    }
}