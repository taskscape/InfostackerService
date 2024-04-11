using Markdig;

namespace ShareAPI.Services;

public class SharingService : ISharingService
{
    private readonly ILogger<SharingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string? _rootFolderPath;
    private readonly string? _templatePath;
    private readonly string? _templateScriptPath;

    public SharingService(ILogger<SharingService> logger, IConfiguration configuration, LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _configuration = configuration;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        _rootFolderPath = _configuration.GetSection("RootFolder").Value;
        _templatePath = _configuration.GetSection("TemplatePath").Value;
        _templateScriptPath = _configuration.GetSection("TemplateScriptPath").Value;
    }

    public async Task<Guid> UploadMarkdownWithFiles(string markdown, List<IFormFile> files)
    {
        var identifier = Guid.NewGuid();

        // Create a directory with the GUID as its name
        var directoryPath = Path.Combine(_rootFolderPath, identifier.ToString());
        Directory.CreateDirectory(directoryPath);

        // Save the markdown content to a file within this directory
        var markdownFilePath = Path.Combine(directoryPath, "content.md");
        await File.WriteAllTextAsync(markdownFilePath, markdown);

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
        return identifier;
    }

    public Task<string> GetMarkdownContent(string identifier)
    {
        // Construct the path to the markdown file using the provided GUID
        string markdownFilePath = Path.Combine(_rootFolderPath, identifier, "content.md");

        // Check if the file exists
        if (!File.Exists(markdownFilePath))
        {
            return Task.FromResult<string>(null);
        }

        // Read the content of the markdown file
        var markdownContent = File.ReadAllText(markdownFilePath);

        // Convert markdown string to HTML
        var htmlContent = Markdown.ToHtml(markdownContent);
        
        // Reading templates
        var templatePath = _templatePath;
        var scriptTemplatePath = _templateScriptPath;
        var htmlTemplate = File.ReadAllText(templatePath);
        var scriptTemplate = File.ReadAllText(scriptTemplatePath);
        
        // Inserting markdown content into html
        htmlTemplate = htmlTemplate.Replace("{markdown}", htmlContent);
        
        // Getting PDFs
        var pdfFiles = Directory.GetFiles(Path.Combine(_rootFolderPath, identifier))
            .Where(file => Path.GetExtension(file) == ".pdf");
        
        var pdfUrls = pdfFiles.Select(file =>
        {
            var fileName = Path.GetFileName(file);
            var url = _linkGenerator.GetPathByAction("GetPdf", "Sharing", new { identifier, fileName });
            var scheme = _httpContextAccessor.HttpContext.Request.Scheme;
            var host = _httpContextAccessor.HttpContext.Request.Host.ToString();
            return $"{scheme}://{host}{url}";
        });
        
        var scripts = string.Empty;
        // Adding PDFs to script
        foreach (var pdfUrl in pdfUrls)
        {
            scriptTemplate = scriptTemplate.Replace("{pdfUrl}", $"\"{pdfUrl}\"");
            scriptTemplate = scriptTemplate.Replace("{pdfDivId}", $"{Guid.NewGuid()}");
            scriptTemplate = scriptTemplate.Replace("{pdfName}", $"\"{Path.GetFileName(pdfUrl)}\"");
            scriptTemplate = scriptTemplate.Replace("{token}", $"\"{_configuration.GetSection("AdobeAPIToken").Value}\"");
            scripts += scriptTemplate;
            scriptTemplate = File.ReadAllText(scriptTemplatePath);
        }
        
        // Inserting PDFs into html
        htmlTemplate = htmlTemplate.Replace("{pdfList}", scripts);
        
        // Getting docs
        var docs = string.Empty;
        var docFiles = Directory.GetFiles(Path.Combine(_rootFolderPath, identifier))
            .Where(file => Path.GetExtension(file) == ".doc" || Path.GetExtension(file) == ".docx");
        
        var docUrls = docFiles.Select(file =>
        {
            var fileName = Path.GetFileName(file);
            var url = _linkGenerator.GetPathByAction("GetDoc", "Sharing", new { identifier, fileName });
            var scheme = _httpContextAccessor.HttpContext.Request.Scheme;
            var host = _httpContextAccessor.HttpContext.Request.Host.ToString();
            return $"{scheme}://{host}{url}";
        });

        // Adding iframes for each doc
        foreach (var docUrl in docUrls)
        {
            docs += $"<iframe src=\"https://docs.google.com/viewer?url={docUrl}&embedded=true\" style=\"width: 800px; height:1400px;\"></iframe>\n";
        }
        
        // Inserting the iframes into html
        htmlTemplate = htmlTemplate.Replace("{docs}", docs);
        
        var images = string.Empty;
        var acceptedFormats = new List<string>
        {
            ".jpg",
            ".jpeg",
            ".png"
        };
        var imageFiles = Directory.GetFiles(Path.Combine(_rootFolderPath, identifier))
            .Where(file => acceptedFormats.Contains(Path.GetExtension(file)));

        var imagePaths = imageFiles.Select(file =>
        {
            var fileName = Path.GetFileName(file);
            var url = _linkGenerator.GetPathByAction("GetImage", "Sharing", new { identifier, fileName });
            var scheme = _httpContextAccessor.HttpContext.Request.Scheme;
            var host = _httpContextAccessor.HttpContext.Request.Host.ToString();
            return $"{scheme}://{host}{url}";
        });

        foreach (var image in imagePaths)
        {
            images += $"<img src={image}>\n";
        }
        htmlTemplate = htmlTemplate.Replace("{images}", images);
        return Task.FromResult(htmlTemplate);
    }

    public async Task<bool> UpdateMarkdownWithFiles(string markdown, List<IFormFile> files, Guid identifier)
    {
        // Create a directory with the GUID as its name
        var directoryPath = Path.Combine(_rootFolderPath, identifier.ToString());
        if (!Directory.Exists(directoryPath))
        {
            return false;
        }
        Directory.Delete(directoryPath, true);
        Directory.CreateDirectory(directoryPath);
            
        // Save the markdown content to a file within this directory
        var markdownFilePath = Path.Combine(directoryPath, "content.md");
        await File.WriteAllTextAsync(markdownFilePath, markdown);

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
        return true;
    }

    public Task<bool> DeleteMarkdownWithFiles(string identifier)
    {
        var directoryPath = Path.Combine(_rootFolderPath, identifier);

        if (!Directory.Exists(directoryPath))
        {
            return Task.FromResult(false);
        }

        Directory.Move(directoryPath, directoryPath + "-deleted");
        return Task.FromResult(true);
    }

    public Task<FileStream> GetPdf(string identifier, string fileName)
    {
        var filePath = Path.Combine(_rootFolderPath, identifier, fileName);
            
        return !File.Exists(filePath) ? Task.FromResult<FileStream>(null) : Task.FromResult(new FileStream(filePath, FileMode.Open, FileAccess.Read));
    }

    public Task<FileStream> GetDoc(string identifier, string fileName)
    {
        var filePath = Path.Combine(_rootFolderPath, identifier, fileName);
            
        return !File.Exists(filePath) ? Task.FromResult<FileStream>(null) : Task.FromResult(new FileStream(filePath, FileMode.Open, FileAccess.Read));
    }

    public Task<FileStream> GetImage(string identifier, string fileName)
    {
        var filePath = Path.Combine(_rootFolderPath, identifier, fileName);
            
        return !File.Exists(filePath) ? Task.FromResult<FileStream>(null) : Task.FromResult(new FileStream(filePath, FileMode.Open, FileAccess.Read));
    }
}