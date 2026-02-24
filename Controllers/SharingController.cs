using System.Net;
using Infostacker.Services;
using Microsoft.AspNetCore.Mvc;

namespace Infostacker.Controllers;

[ApiController]
[Route("sharing")]
public class SharingController : ControllerBase
{
    private readonly ISharingService _sharingService;
    private readonly ILogger<SharingController> _logger;

    public SharingController(ISharingService sharingService, ILogger<SharingController> logger)
    {
        _sharingService = sharingService;
        _logger = logger;
    }

    [HttpPost("uploadmarkdownwithfiles")]
    [RateLimit(100, 86400)]
    public async Task<IActionResult> UploadMarkdownWithFiles([FromForm] string markdown, [FromForm] List<IFormFile> files)
    {
        Guid identifier = await _sharingService.UploadMarkdownWithFiles(markdown, files);
        if (identifier == Guid.Empty)
        {
            return BadRequest(new { Message = "There was an error while processing the request.", id = identifier });
        }

        return Ok(new { Message = "Markdown and files uploaded successfully.", id = identifier });
    }

    [HttpGet("{identifier}")]
    public async Task<IActionResult> GetMarkdownContent(string identifier)
    {
        string? result = await _sharingService.GetMarkdownContent(identifier);
        if (result is null)
        {
            return LogAndReturnNotFound("No markdown or files with given identifier found.", identifier);
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
            return LogAndReturnNotFound("No markdown or files with given identifier found.", identifier.ToString());
        }

        return Ok(new { Message = "Markdown and files updated successfully.", id = identifier });
    }

    [HttpDelete("{identifier}")]
    public async Task<IActionResult> DeleteMarkdownWithFiles(string identifier)
    {

        if (!await _sharingService.DeleteMarkdownWithFiles(identifier))
        {
            return LogAndReturnNotFound("No markdown or files with given identifier found.", identifier);
        }

        return Ok(new { Message = "Markdown and files successfully deleted.", id = identifier });
    }

    [HttpGet("pdf/{identifier}/{fileName}")]
    public async Task<IActionResult> GetPdf(string identifier, string fileName)
    {
        FileStream stream = await _sharingService.GetPdf(identifier, fileName);
        if (stream is null)
        {
            return LogAndReturnNotFound("PDF does not exist.", identifier, fileName);
        }
        return File(stream, "application/pdf");
    }

    [HttpGet("doc/{identifier}/{fileName}")]
    public async Task<IActionResult> GetDoc(string identifier, string fileName)
    {
        FileStream stream = await _sharingService.GetDoc(identifier, fileName);
        if (stream is null)
        {
            return LogAndReturnNotFound("Doc does not exist.", identifier, fileName);
        }
        return File(stream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    }

    [HttpGet("image/{identifier}/{fileName}")]
    public async Task<IActionResult> GetImage(string identifier, string fileName)
    {
        FileStream stream = await _sharingService.GetImage(identifier, fileName);
        if (stream is null)
        {
            return LogAndReturnNotFound("Image does not exist.", identifier, fileName);
        }
        return File(stream, "image/png");
    }
    
    [HttpGet("video/{identifier}/{fileName}")]
    public async Task<IActionResult> GetVideo(string identifier, string fileName)
    {
        FileStream stream = await _sharingService.GetVideo(identifier, fileName);
        if (stream is null)
        {
            return LogAndReturnNotFound("Video does not exist.", identifier, fileName);
        }
        return File(stream, "video/mp4");
    }
    
    [HttpGet("version")]
    public async Task<IActionResult> GetVersion()
    {
        return Ok(await _sharingService.GetVersion());
    }

    private IActionResult LogAndReturnNotFound(string message, string identifier, string? fileName = null)
    {
        _logger.LogWarning(
            "Requested resource was not found. Message: {Message}. Identifier: {Identifier}. FileName: {FileName}. Path: {Path}. TraceId: {TraceId}",
            message,
            identifier,
            fileName,
            HttpContext.Request.Path.Value,
            HttpContext.TraceIdentifier);

        return NotFound(new { Message = message, id = identifier });
    }
}
