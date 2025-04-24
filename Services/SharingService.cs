using System.Net;
using System.Text.RegularExpressions;
using Markdig;

namespace Infostacker.Services;

public partial class SharingService : ISharingService
{
    private readonly ILogger<SharingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;
    public required string? NotesFolderPath;
    public required string? TemplatePath;
    public required string? TemplateScriptPath;
    public required string? BuildVersion;
    public required int? MaxFileSize;

    public SharingService(ILogger<SharingService> logger, IConfiguration configuration, LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _configuration = configuration;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        NotesFolderPath = _configuration.GetSection("NotesFolder").Value ?? string.Empty;
        TemplatePath = _configuration.GetSection("TemplatePath").Value ?? string.Empty;
        TemplateScriptPath = _configuration.GetSection("TemplateScriptPath").Value ?? string.Empty;
        BuildVersion = _configuration.GetSection("version").Value ?? string.Empty;
        MaxFileSize = int.Parse(_configuration.GetSection("MaxFileSizeInBytes").Value);
    }

    public async Task<Guid> UploadMarkdownWithFiles(string markdown, List<IFormFile> files)
    {
        Guid identifier = Guid.NewGuid();

        if (files.Any(file => file.Length > MaxFileSize))
        {
            _logger.LogInformation("File exceeded max size, discarding note.");
            return Guid.Empty;
        }

        // Create a directory with the GUID as its name
        string directoryPath = Path.Combine(NotesFolderPath, identifier.ToString());
        try
        {
            Directory.CreateDirectory(directoryPath);
        }
        catch (Exception e)
        {
            _logger.LogError($"Error creating directory: {e}");
            return Guid.Empty;
        }

        await SaveFiles(markdown, files, directoryPath);
        return identifier;
    }

    public Task<string> GetMarkdownContent(string identifier)
    {
        // Construct the path to the markdown file using the provided GUID
        string markdownFilePath = Path.Combine(NotesFolderPath, identifier, "content.md");

        // Check if the file exists
        if (!File.Exists(markdownFilePath))
        {
            return Task.FromResult<string>(null);
        }
        _logger.LogInformation("Markdown file found in \"{path}\"", markdownFilePath);

        // Read the content of the markdown file
        string markdownContent = File.ReadAllText(markdownFilePath);

        // Convert markdown string to HTML
        string htmlContent = Markdown.ToHtml(markdownContent);

        // Reading templates
        string? templatePath = TemplatePath;
        string? scriptTemplatePath = TemplateScriptPath;
        string htmlTemplate = File.ReadAllText(templatePath);
        string scriptTemplate = File.ReadAllText(scriptTemplatePath);
        Regex regex = FileAttachmentRegex();

        // Setting note page title
        try
        {
            string[] markdownLines = markdownContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
            string noteTitle = markdownLines.Length > 0 ? markdownLines[0].TrimEnd() : string.Empty;

            if (regex.Matches(noteTitle).Any())
            {
                markdownLines = new[] { "Untitled", "\n" }.Concat(markdownLines).ToArray();
                noteTitle = "Untitled";
            }
    
            _logger.LogInformation("Title extracted from markdown as \"{title}\"", noteTitle);
            htmlTemplate = htmlTemplate.Replace("{title}", noteTitle);
            
            string[] htmlLines = htmlTemplate.Split([Environment.NewLine], StringSplitOptions.None);
            _logger.LogInformation("Title line after replacement action: {titleLine}", htmlLines[3]);
        }
        catch (Exception e)
        {
            _logger.LogError("Error while replacing title: \"{error}\"", e);
        }

        // Inserting markdown content into html
        _logger.LogInformation("Replacing markdown placeholder tag with html");
        htmlTemplate = htmlTemplate.Replace("{markdown}", htmlContent);

        // Getting PDFs
        IEnumerable<string> pdfFiles = Directory.GetFiles(Path.Combine(NotesFolderPath, identifier))
            .Where(file => Path.GetExtension(file).Equals(".pdf", StringComparison.InvariantCultureIgnoreCase));

        IEnumerable<string> pdfUrls = pdfFiles.Select(file =>
        {
            string fileName = Path.GetFileName(file);
            string? url = _linkGenerator.GetPathByAction("GetPdf", "sharing", new { identifier, fileName });
            string scheme = _httpContextAccessor.HttpContext.Request.Scheme;
            string host = _httpContextAccessor.HttpContext.Request.Host.ToString();
            return Uri.UnescapeDataString($"{scheme}://{host}{url}");
        });
        
        // Adding PDFs to template
        foreach (string pdfUrl in pdfUrls)
        {
            scriptTemplate = scriptTemplate.Replace("{pdfUrl}", $"\"{pdfUrl}\"");
            MatchCollection matches = regex.Matches(htmlTemplate);
            foreach (Match match in matches)
            {
                // check if the match value contains the file name without the first 9 random characters
                string sanitizedMatch = Regex.Replace(WebUtility.HtmlDecode(match.Value), @"[^a-zA-Z0-9().\- ]", "_");
                if (!sanitizedMatch.Contains(Path.GetFileName(pdfUrl)[9..])) continue;
                htmlTemplate = ReplaceFirstOccurrence(htmlTemplate, match.Value, scriptTemplate);
                break;
            }
            
            scriptTemplate = File.ReadAllText(scriptTemplatePath);
        }

        // Getting docs
        IEnumerable<string> docFiles = Directory.GetFiles(Path.Combine(NotesFolderPath, identifier))
            .Where(file => Path.GetExtension(file).Equals(".doc", StringComparison.InvariantCultureIgnoreCase) || 
                           Path.GetExtension(file).Equals(".docx", StringComparison.InvariantCultureIgnoreCase));

        IEnumerable<string> docUrls = docFiles.Select(file =>
        {
            string fileName = Path.GetFileName(file);
            string? url = _linkGenerator.GetPathByAction("GetDoc", "sharing", new { identifier, fileName });
            string scheme = _httpContextAccessor.HttpContext.Request.Scheme;
            string host = _httpContextAccessor.HttpContext.Request.Host.ToString();
            return Uri.UnescapeDataString($"{scheme}://{host}{url}");
        });

        // Adding docs to template
        foreach (string docUrl in docUrls)
        {
            MatchCollection matches = regex.Matches(htmlTemplate);
            foreach (Match match in matches)
            {
                // check if the match value contains the file name without the first 9 random characters
                string sanitizedMatch = Regex.Replace(WebUtility.HtmlDecode(match.Value), @"[^a-zA-Z0-9().\- ]", "_");
                if (!sanitizedMatch.Contains(Path.GetFileName(docUrl)[9..])) continue;
                htmlTemplate = ReplaceFirstOccurrence(htmlTemplate, match.Value, $"<div class=\"docs-container\"><iframe src=\"https://docs.google.com/viewer?url={docUrl}&embedded=true\"></iframe></div>\n");
                break;
            }
        }
        List<string> acceptedImageFormats =
        [
            ".jpg",
            ".jpeg",
            ".png"
        ];
        // Getting images
        IEnumerable<string> imageFiles = Directory.GetFiles(Path.Combine(NotesFolderPath, identifier))
            .Where(file => acceptedImageFormats.Contains(Path.GetExtension(file).ToLowerInvariant()));

        IEnumerable<string> imagePaths = imageFiles.Select(file =>
        {
            string fileName = Path.GetFileName(file);
            string? url = _linkGenerator.GetPathByAction("GetImage", "sharing", new { identifier, fileName });
            string scheme = _httpContextAccessor.HttpContext.Request.Scheme;
            string host = _httpContextAccessor.HttpContext.Request.Host.ToString();
            return $"{scheme}://{host}{url}";
        });

        // Adding images to template
        foreach (string imagePath in imagePaths)
        {
            MatchCollection matches = regex.Matches(htmlTemplate);
            foreach (Match match in matches)
            {
                // check if the match value contains the file name without the first 9 random characters
                string sanitizedMatch = Regex.Replace(WebUtility.HtmlDecode(match.Value), @"[^a-zA-Z0-9().\- ]", "_");
                if (!sanitizedMatch.Contains(Path.GetFileName(Uri.UnescapeDataString(imagePath))[9..])) continue;
                htmlTemplate = ReplaceFirstOccurrence(htmlTemplate, match.Value, $"<div class=\"image-container\"><img src=\"{imagePath}\"></div>\n");
                break;
            }
        }

        List<string> acceptedVideoFormats = [".mp4"];
        // Getting videos
        IEnumerable<string> videoFiles = Directory.GetFiles(Path.Combine(NotesFolderPath, identifier))
            .Where(file => acceptedVideoFormats.Contains(Path.GetExtension(file).ToLowerInvariant()));

        IEnumerable<string> videoPaths = videoFiles.Select(file =>
        {
            string fileName = Path.GetFileName(file);
            string? url = _linkGenerator.GetPathByAction("GetVideo", "sharing", new { identifier, fileName });
            string scheme = _httpContextAccessor.HttpContext.Request.Scheme;
            string host = _httpContextAccessor.HttpContext.Request.Host.ToString();
            return $"{scheme}://{host}{url}";
        });

        // Adding videos to template
        foreach (string videoPath in videoPaths)
        {
            MatchCollection matches = regex.Matches(htmlTemplate);
            foreach (Match match in matches)
            {
                // check if the match value contains the file name without the first 9 random characters
                string sanitizedMatch = Regex.Replace(WebUtility.HtmlDecode(match.Value), @"[^a-zA-Z0-9().\- ]", "_");
                if (!sanitizedMatch.Contains(Path.GetFileName(Uri.UnescapeDataString(videoPath))[9..])) continue;
                htmlTemplate = ReplaceFirstOccurrence(htmlTemplate, match.Value, $"<video class=\"video-container\" controls><source src={videoPath} type=\"video/mp4\"></video>\n");
                break;
            }
        }
        return Task.FromResult(htmlTemplate);
    }

    public async Task<bool> UpdateMarkdownWithFiles(string markdown, List<IFormFile> files, Guid identifier)
    {
        if (files.Any(file => file.Length > MaxFileSize))
        {
            _logger.LogInformation("File exceeded max size, discarding note.");
            return false;
        }
        
        // Create a directory with the GUID as its name
        string directoryPath = Path.Combine(NotesFolderPath, identifier.ToString());
        if (!Directory.Exists(directoryPath))
        {
            return false;
        }
        Directory.Delete(directoryPath, true);
        Directory.CreateDirectory(directoryPath);

        await SaveFiles(markdown, files, directoryPath);
        return true;
    }

    public Task<bool> DeleteMarkdownWithFiles(string identifier)
    {
        string directoryPath = Path.Combine(NotesFolderPath, identifier);

        if (!Directory.Exists(directoryPath))
        {
            return Task.FromResult(false);
        }

        Directory.Move(directoryPath, directoryPath + "-deleted");
        return Task.FromResult(true);
    }

    public Task<FileStream> GetPdf(string identifier, string fileName)
    {
        string filePath = Path.Combine(NotesFolderPath, identifier, fileName);

        return !File.Exists(filePath) ? Task.FromResult<FileStream>(null) : Task.FromResult(new FileStream(filePath, FileMode.Open, FileAccess.Read));
    }

    public Task<FileStream> GetDoc(string identifier, string fileName)
    {
        string filePath = Path.Combine(NotesFolderPath, identifier, fileName);

        return !File.Exists(filePath) ? Task.FromResult<FileStream>(null) : Task.FromResult(new FileStream(filePath, FileMode.Open, FileAccess.Read));
    }

    public Task<FileStream> GetImage(string identifier, string fileName)
    {
        string filePath = Path.Combine(NotesFolderPath, identifier, fileName);

        return !File.Exists(filePath) ? Task.FromResult<FileStream>(null) : Task.FromResult(new FileStream(filePath, FileMode.Open, FileAccess.Read));
    }
    
    public Task<FileStream> GetVideo(string identifier, string fileName)
    {
        string filePath = Path.Combine(NotesFolderPath, identifier, fileName);

        return !File.Exists(filePath) ? Task.FromResult<FileStream>(null) : Task.FromResult(new FileStream(filePath, FileMode.Open, FileAccess.Read));
    }

    public Task<object> GetVersion()
    {
        var versionInfo = new
        {
            Version = BuildVersion,
            CompilationDate = File.GetLastAccessTime(GetType().Assembly.Location)
        };
        return Task.FromResult<object>(versionInfo);
    }

    private async Task SaveFiles(string markdown, List<IFormFile> files, string directoryPath)
    {
        // Save the markdown content to a file within this directory
        string markdownFilePath = Path.Combine(directoryPath, "content.md");
        await File.WriteAllTextAsync(markdownFilePath, markdown);

        _logger.LogInformation($"Markdown saved to: {markdownFilePath}");

        long totalBytes = 0;
        foreach (IFormFile file in files)
        {
            // Log the file name
            _logger.LogInformation($"Received file: {file.FileName}");

            string filename = Regex.Replace(file.FileName, @"[^a-zA-Z0-9().\- ]", "_");
            //string filename = file.FileName;

            // Save each file in the directory
            Random random = new();
            string uniqueFileName = $"{random.Next():x}-{filename}";
            string filePath = Path.Combine(directoryPath, uniqueFileName);

            await using (FileStream stream = new(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream).ConfigureAwait(false);
                
            }
            string fileContent = await File.ReadAllTextAsync(filePath);
            if (IsBase64String(fileContent))
            {
                byte[] binary = Convert.FromBase64String(fileContent);
                await File.WriteAllBytesAsync(filePath, binary);
            }

            totalBytes += file.Length;
        }

        _logger.LogInformation($"Total bytes received: {totalBytes}");
    }
    
    private static bool IsBase64String(string base64)
    {
        Span<byte> buffer = new(new byte[base64.Length]);
        return Convert.TryFromBase64String(base64, buffer , out int _);
    }
    
    private static string ReplaceFirstOccurrence(string source, string oldValue, string newValue)
    {
        int pos = source.IndexOf(oldValue);
        if (pos < 0)
        {
            return source;
        }
        return source[..pos] + newValue + source[(pos + oldValue.Length)..];
    }

    [GeneratedRegex(@"!\[\[.*?\]\]")]
    private static partial Regex FileAttachmentRegex();
}