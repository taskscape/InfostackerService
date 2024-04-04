using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ShareAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SharingController : ControllerBase
    {

        private readonly ILogger<SharingController> _logger;

        public SharingController(ILogger<SharingController> logger)
        {
            _logger = logger;
        }

        [HttpPost("UploadMarkdownWithFiles")]
        public async Task<IActionResult> UploadMarkdownWithFiles([FromForm] string markdown, [FromForm] List<IFormFile> files)
        {
            // Generate a new GUID
            var guid = Guid.NewGuid().ToString();

            // Create a directory with the GUID as its name
            var directoryPath = Path.Combine("YourBaseDirectory", guid);
            Directory.CreateDirectory(directoryPath);

            // Save the markdown content to a file within this directory
            var markdownFilePath = Path.Combine(directoryPath, "content.md");
            await System.IO.File.WriteAllTextAsync(markdownFilePath, markdown);

            _logger.LogInformation($"Markdown saved to: {markdownFilePath}");

            long totalBytes = 0;
            foreach (var file in files)
            {
                // Log the file name
                _logger.LogInformation($"Received file: {file.FileName}");

                // Save each file in the directory
                var filePath = Path.Combine(directoryPath, file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                totalBytes += file.Length;
            }

            _logger.LogInformation($"Total bytes received: {totalBytes}");

            // Return a response indicating success
            return Ok(new { Message = "Markdown and files uploaded successfully.", Guid = guid });
        }
    }
}