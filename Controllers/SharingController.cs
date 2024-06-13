using System.Net;
using Microsoft.AspNetCore.Mvc;
using ShareAPI.Services;

namespace ShareAPI.Controllers;

[ApiController]
[Route("sharing")]
public class SharingController : ControllerBase
{
    private readonly ISharingService _sharingService;

    public SharingController(ISharingService sharingService)
    {
        _sharingService = sharingService;
    }

    [HttpPost("upload")]
    [RateLimit(100, 86400)]
    public async Task<IActionResult> UploadMarkdownWithFiles([FromForm] string markdown, [FromForm] List<IFormFile> files)
    {
        Guid identifier = await _sharingService.UploadMarkdownWithFiles(markdown, files);
        if (identifier == Guid.Empty)
        {
            BadRequest(new { Message = "There was an error while processing the request.", id = identifier });
        }

        return Ok(new { Message = "Markdown and files uploaded successfully.", id = identifier });
    }

    [HttpGet("{identifier}")]
    public async Task<IActionResult> GetMarkdownContent(string identifier)
    {
        string? result = await _sharingService.GetMarkdownContent(identifier);
        if (result is null)
        {
            return NotFound(new { Message = "No markdown or files with given identifier found.", id = identifier });
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
            return NotFound(new { Message = "No markdown or files with given identifier found.", id = identifier });
        }

        return Ok(new { Message = "Markdown and files updated successfully.", id = identifier });
    }

    [HttpDelete("{identifier}")]
    public async Task<IActionResult> DeleteMarkdownWithFiles(string identifier)
    {

        if (!await _sharingService.DeleteMarkdownWithFiles(identifier))
        {
            return NotFound(new { Message = "No markdown or files with given identifier found.", id = identifier });
        }

        return Ok(new { Message = "Markdown and files successfully deleted.", id = identifier });
    }

    [HttpGet("pdf/{identifier}/{fileName}")]
    public async Task<IActionResult> GetPdf(string identifier, string fileName)
    {
        FileStream stream = await _sharingService.GetPdf(identifier, fileName);
        if (stream is null)
        {
            return NotFound(new { Message = "PDF does not exist.", id = identifier });
        }
        return File(stream, "application/pdf");
    }

    [HttpGet("doc/{identifier}/{fileName}")]
    public async Task<IActionResult> GetDoc(string identifier, string fileName)
    {
        FileStream stream = await _sharingService.GetDoc(identifier, fileName);
        if (stream is null)
        {
            return NotFound(new { Message = "Doc does not exist.", id = identifier });
        }
        return File(stream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    }

    [HttpGet("image/{identifier}/{fileName}")]
    public async Task<IActionResult> GetImage(string identifier, string fileName)
    {
        FileStream stream = await _sharingService.GetImage(identifier, fileName);
        if (stream is null)
        {
            return NotFound(new { Message = "Image does not exist.", id = identifier });
        }
        return File(stream, "image/png");
    }
    
    [HttpGet("version")]
    public async Task<IActionResult> GetVersion()
    {
        return Ok(await _sharingService.GetVersion());
    }
}