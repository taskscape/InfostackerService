using Microsoft.AspNetCore.Mvc;
using Markdig;

namespace ShareAPI.Controllers
{
    [ApiController]
    [Route("[controller]/{guid?}")] // Make GUID optional at the controller level
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
            string identifier = Guid.NewGuid().ToString();

            // Create a directory with the GUID as its name
            string directoryPath = Path.Combine("YourBaseDirectory", identifier);
            Directory.CreateDirectory(directoryPath);

            // Save the markdown content to a file within this directory
            string markdownFilePath = Path.Combine(directoryPath, "content.md");
            await System.IO.File.WriteAllTextAsync(markdownFilePath, markdown);

            _logger.LogInformation($"Markdown saved to: {markdownFilePath}");

            long totalBytes = 0;
            foreach (IFormFile file in files)
            {
                // Log the file name
                _logger.LogInformation($"Received file: {file.FileName}");

                // Save each file in the directory
                string filePath = Path.Combine(directoryPath, file.FileName);
                await using (FileStream stream = new(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream).ConfigureAwait(false);
                }

                totalBytes += file.Length;
            }

            _logger.LogInformation($"Total bytes received: {totalBytes}");

            // Return a response indicating success
            return Ok(new { Message = "Markdown and files uploaded successfully.", Guid = identifier });
        }

        [HttpGet("")]
        public IActionResult GetMarkdownContent(string guid)
        {
            // Construct the path to the markdown file using the provided GUID
            string markdownFilePath = Path.Combine("YourBaseDirectory", guid, "content.md");

            // Check if the file exists
            if (!System.IO.File.Exists(markdownFilePath))
            {
                // If the file does not exist, return a 404 Not Found response
                return NotFound(new { Message = "Markdown file not found." });
            }

            // Read the content of the markdown file
            string markdownContent = System.IO.File.ReadAllText(markdownFilePath);

            // Convert markdown string to HTML
            string htmlContent = Markdown.ToHtml(markdownContent);

            // Return the content of the markdown file
            return Ok(new { Content = htmlContent });
        }
    }
}