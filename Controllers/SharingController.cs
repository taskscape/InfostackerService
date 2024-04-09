using System.Net;
using Microsoft.AspNetCore.Mvc;
using Markdig;
using Microsoft.Extensions.Primitives;

namespace ShareAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SharingController : ControllerBase
    {

        private readonly ILogger<SharingController> _logger;
        private readonly IConfiguration _configuration;
        private readonly string? _rootFolderPath;

        public SharingController(ILogger<SharingController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _rootFolderPath = _configuration.GetSection("RootFolder").Value;
        }
        
        [HttpPost("UploadMarkdownWithFiles")]
        public async Task<IActionResult> UploadMarkdownWithFiles([FromForm] string markdown, [FromForm] List<IFormFile> files)
        {
            // Generate a new GUID
            var identifier = Guid.NewGuid();

            // Create a directory with the GUID as its name
            var directoryPath = Path.Combine(_rootFolderPath, identifier.ToString());
            Directory.CreateDirectory(directoryPath);

            // Save the markdown content to a file within this directory
            var markdownFilePath = Path.Combine(directoryPath, "content.md");
            await System.IO.File.WriteAllTextAsync(markdownFilePath, markdown);

            _logger.LogInformation($"Markdown saved to: {markdownFilePath}");

            long totalBytes = 0;
            foreach (IFormFile file in files)
            {
                // Log the file name
                _logger.LogInformation($"Received file: {file.FileName}");

                // Save each file in the directory
                var filePath = Path.Combine(directoryPath, file.FileName);
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

        [HttpGet("{identifier}")]
        public IActionResult GetMarkdownContent(string identifier)
        {
            // Construct the path to the markdown file using the provided GUID
            string markdownFilePath = Path.Combine(_rootFolderPath, identifier, "content.md");

            // Check if the file exists
            if (!System.IO.File.Exists(markdownFilePath))
            {
                // If the file does not exist, return a 404 Not Found response
                return NotFound(new { Message = "Markdown file not found." });
            }

            // Read the content of the markdown file
            var markdownContent = System.IO.File.ReadAllText(markdownFilePath);

            // Convert markdown string to HTML
            var htmlContent = Markdown.ToHtml(markdownContent);
            var templatePath = "template.html";
            var htmlTemplate = System.IO.File.ReadAllText(templatePath);
            
            var pdfUrls = Directory.GetFiles(Path.Combine(_rootFolderPath, identifier))
                .Where(file => Path.GetExtension(file) == ".pdf")
                .Select(file => Url.Action("GetPdf", "Sharing", new { identifier = identifier, fileName = Path.GetFileName(file) }, Request.Scheme));
            
            foreach (var pdfUrl in pdfUrls)
            {
                htmlTemplate = htmlTemplate.Replace("{pdfUrl}", $"\"{pdfUrl}\"");
                htmlTemplate = htmlTemplate.Replace("{pdfName}", $"\"{Path.GetFileName(pdfUrl)}\"");
                htmlTemplate = htmlTemplate.Replace("{token}", $"\"{_configuration.GetSection("AdobeAPIToken").Value}\"");
            }

            htmlTemplate = htmlTemplate.Replace("{markdown}", htmlContent);

            return new ContentResult {
                ContentType = "text/html",
                StatusCode = (int)HttpStatusCode.OK,
                Content = htmlTemplate
            };
        }
        
        [HttpPut("{identifier}")]
        public async Task<IActionResult> UpdateMarkdownWithFiles([FromForm] string markdown, [FromForm] List<IFormFile> files, Guid identifier)
        {
            // Create a directory with the GUID as its name
            var directoryPath = Path.Combine(_rootFolderPath, identifier.ToString());
            if (!Directory.Exists(directoryPath))
            {
                return NotFound(new { Message = "No markdown or files with given identifier found.", Guid = identifier });
            }
            Directory.Delete(directoryPath, true);
            Directory.CreateDirectory(directoryPath);
            
            // Save the markdown content to a file within this directory
            var markdownFilePath = Path.Combine(directoryPath, "content.md");
            await System.IO.File.WriteAllTextAsync(markdownFilePath, markdown);

            _logger.LogInformation($"Markdown saved to: {markdownFilePath}");

            long totalBytes = 0;
            foreach (IFormFile file in files)
            {
                // Log the file name
                _logger.LogInformation($"Received file: {file.FileName}");

                // Save each file in the directory
                var filePath = Path.Combine(directoryPath, file.FileName);
                await using (FileStream stream = new(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream).ConfigureAwait(false);
                }

                totalBytes += file.Length;
            }

            _logger.LogInformation($"Total bytes received: {totalBytes}");

            // Return a response indicating success
            return Ok(new { Message = "Markdown and files updated successfully.", Guid = identifier });
        }
        
        [HttpDelete("{identifier}")]
        public IActionResult DeleteMarkdownWithFiles(string identifier)
        {
            var directoryPath = Path.Combine(_rootFolderPath, identifier);

            if (!Directory.Exists(directoryPath))
            {
                return NotFound(new { Message = "No markdown or files with given identifier found.", Guid = identifier });
            }

            Directory.Move(directoryPath, directoryPath + "-deleted");
            return Ok(new { Message = "Markdown and files successfully deleted.", Guid = identifier });
        }
        
        [HttpGet("Pdf/{identifier}/{fileName}")]
        public IActionResult GetPdf(string identifier, string fileName)
        {

            string filePath = Path.Combine(_rootFolderPath, identifier, fileName);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { Message = "PDF file not found." });
            }
            
            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return File(stream, "application/pdf");
        }
    }
}