using System.Net;
using System.Text.RegularExpressions;
using Markdig;

namespace Infostacker.Services;

public partial class SharingService : ISharingService
{
    private static readonly MarkdownPipeline SafeMarkdownPipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .Build();
    private static readonly HashSet<string> AcceptedImageFormats = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif" };
    private static readonly HashSet<string> AcceptedVideoFormats = new(StringComparer.OrdinalIgnoreCase) { ".mp4" };
    private static readonly HashSet<string> AcceptedDocFormats = new(StringComparer.OrdinalIgnoreCase) { ".doc", ".docx" };
    private static readonly HashSet<string> AcceptedPdfFormats = new(StringComparer.OrdinalIgnoreCase) { ".pdf" };
    private static readonly HashSet<string> AcceptedUploadFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".doc",
        ".docx",
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".mp4"
    };

    private readonly ILogger<SharingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _notesFolderPath;
    private readonly string _templatePath;
    private readonly string _templateScriptPath;
    private readonly string _buildVersion;
    private readonly int _maxFileSize;
    private readonly int _maxFilesPerUpload;
    private readonly long _maxTotalUploadBytes;
    private readonly int _maxMarkdownLength;

    public SharingService(
        ILogger<SharingService> logger,
        IConfiguration configuration,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _configuration = configuration;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;

        _notesFolderPath = GetRequiredPathSetting("NotesFolder");
        _templatePath = GetRequiredPathSetting("TemplatePath");
        _templateScriptPath = GetRequiredPathSetting("TemplateScriptPath");
        _buildVersion = _configuration.GetValue<string>("version") ?? string.Empty;
        _maxFileSize = _configuration.GetValue<int?>("MaxFileSizeInBytes") is > 0 ? _configuration.GetValue<int>("MaxFileSizeInBytes") : 104857600;
        _maxFilesPerUpload = _configuration.GetValue<int?>("MaxFilesPerUpload") is > 0 ? _configuration.GetValue<int>("MaxFilesPerUpload") : 20;
        _maxTotalUploadBytes = _configuration.GetValue<long?>("MaxTotalUploadBytes") is > 0 ? _configuration.GetValue<long>("MaxTotalUploadBytes") : 104857600L;
        _maxMarkdownLength = _configuration.GetValue<int?>("MaxMarkdownLength") is > 0 ? _configuration.GetValue<int>("MaxMarkdownLength") : 1_000_000;

        Directory.CreateDirectory(_notesFolderPath);
    }

    public async Task<Guid> UploadMarkdownWithFiles(string markdown, List<IFormFile> files)
    {
        if (!IsUploadRequestValid(markdown, files, out string validationMessage))
        {
            _logger.LogWarning("Upload request rejected. Reason: {Reason}", validationMessage);
            return Guid.Empty;
        }

        Guid identifier = Guid.NewGuid();
        string directoryPath = GetNoteDirectoryPath(identifier);

        try
        {
            Directory.CreateDirectory(directoryPath);
            await SaveFiles(markdown, files, directoryPath).ConfigureAwait(false);
            return identifier;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error while saving uploaded markdown and files for {Identifier}.", identifier);

            TryDeleteDirectory(directoryPath);
            return Guid.Empty;
        }
    }

    public async Task<string?> GetMarkdownContent(Guid identifier)
    {
        string noteDirectoryPath = GetNoteDirectoryPath(identifier);
        string markdownFilePath = Path.Combine(noteDirectoryPath, "content.md");

        if (!File.Exists(markdownFilePath))
        {
            return null;
        }

        if (!File.Exists(_templatePath) || !File.Exists(_templateScriptPath))
        {
            _logger.LogError("Template files are missing. TemplatePath: {TemplatePath}, ScriptTemplatePath: {ScriptTemplatePath}", _templatePath, _templateScriptPath);
            return null;
        }

        string markdownContent = await File.ReadAllTextAsync(markdownFilePath).ConfigureAwait(false);
        string htmlTemplate = await File.ReadAllTextAsync(_templatePath).ConfigureAwait(false);
        string scriptTemplate = await File.ReadAllTextAsync(_templateScriptPath).ConfigureAwait(false);

        Regex regex = FileAttachmentRegex();
        htmlTemplate = htmlTemplate.Replace("{title}", ExtractSafeTitle(markdownContent, regex));
        htmlTemplate = htmlTemplate.Replace("{markdown}", Markdown.ToHtml(markdownContent, SafeMarkdownPipeline));

        IEnumerable<string> noteFiles = Directory.EnumerateFiles(noteDirectoryPath);
        IEnumerable<string> pdfFiles = noteFiles.Where(file => AcceptedPdfFormats.Contains(Path.GetExtension(file)));
        IEnumerable<string> docFiles = noteFiles.Where(file => AcceptedDocFormats.Contains(Path.GetExtension(file)));
        IEnumerable<string> imageFiles = noteFiles.Where(file => AcceptedImageFormats.Contains(Path.GetExtension(file)));
        IEnumerable<string> videoFiles = noteFiles.Where(file => AcceptedVideoFormats.Contains(Path.GetExtension(file)));

        foreach (string pdfFilePath in pdfFiles)
        {
            string fileName = Path.GetFileName(pdfFilePath);
            string pdfUrl = BuildFileUrl("GetPdf", identifier, fileName);
            if (string.IsNullOrWhiteSpace(pdfUrl))
            {
                continue;
            }

            string workingScriptTemplate = scriptTemplate
                .Replace("{pdfUrl}", pdfUrl)
                .Replace("{pdfName}", fileName);

            htmlTemplate = ReplaceAttachmentReference(htmlTemplate, regex, fileName, workingScriptTemplate);
        }

        foreach (string docFilePath in docFiles)
        {
            string fileName = Path.GetFileName(docFilePath);
            string docUrl = BuildFileUrl("GetDoc", identifier, fileName);
            if (string.IsNullOrWhiteSpace(docUrl))
            {
                continue;
            }

            string replacement = $"<div class=\"docs-container\"><iframe src=\"https://docs.google.com/viewer?url={docUrl}&embedded=true\"></iframe></div>\n";
            htmlTemplate = ReplaceAttachmentReference(htmlTemplate, regex, fileName, replacement);
        }

        foreach (string imageFilePath in imageFiles)
        {
            string fileName = Path.GetFileName(imageFilePath);
            string imageUrl = BuildFileUrl("GetImage", identifier, fileName);
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                continue;
            }

            string replacement = $"<div class=\"image-container\"><img src=\"{imageUrl}\"></div>\n";
            htmlTemplate = ReplaceAttachmentReference(htmlTemplate, regex, fileName, replacement);
        }

        foreach (string videoFilePath in videoFiles)
        {
            string fileName = Path.GetFileName(videoFilePath);
            string videoUrl = BuildFileUrl("GetVideo", identifier, fileName);
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                continue;
            }

            string replacement = $"<video class=\"video-container\" controls><source src={videoUrl} type=\"video/mp4\"></video>\n";
            htmlTemplate = ReplaceAttachmentReference(htmlTemplate, regex, fileName, replacement);
        }

        return htmlTemplate;
    }

    public async Task<bool> UpdateMarkdownWithFiles(string markdown, List<IFormFile> files, Guid identifier)
    {
        if (!IsUploadRequestValid(markdown, files, out string validationMessage))
        {
            _logger.LogWarning("Update request rejected for {Identifier}. Reason: {Reason}", identifier, validationMessage);
            return false;
        }

        string directoryPath = GetNoteDirectoryPath(identifier);
        if (!Directory.Exists(directoryPath))
        {
            return false;
        }

        try
        {
            Directory.Delete(directoryPath, recursive: true);
            Directory.CreateDirectory(directoryPath);
            await SaveFiles(markdown, files, directoryPath).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error while updating markdown/files for {Identifier}.", identifier);
            return false;
        }
    }

    public Task<bool> DeleteMarkdownWithFiles(Guid identifier)
    {
        string directoryPath = GetNoteDirectoryPath(identifier);
        if (!Directory.Exists(directoryPath))
        {
            return Task.FromResult(false);
        }

        string deletedDirectoryPath = $"{directoryPath}-deleted-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        try
        {
            Directory.Move(directoryPath, deletedDirectoryPath);
            return Task.FromResult(true);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error while deleting note directory for {Identifier}.", identifier);
            return Task.FromResult(false);
        }
    }

    public Task<FileStream?> GetPdf(Guid identifier, string fileName)
    {
        return GetFileStream(identifier, fileName, AcceptedPdfFormats);
    }

    public Task<FileStream?> GetDoc(Guid identifier, string fileName)
    {
        return GetFileStream(identifier, fileName, AcceptedDocFormats);
    }

    public Task<FileStream?> GetImage(Guid identifier, string fileName)
    {
        return GetFileStream(identifier, fileName, AcceptedImageFormats);
    }

    public Task<FileStream?> GetVideo(Guid identifier, string fileName)
    {
        return GetFileStream(identifier, fileName, AcceptedVideoFormats);
    }

    public Task<object> GetVersion()
    {
        var versionInfo = new
        {
            Version = _buildVersion,
            CompilationDate = File.GetLastWriteTime(GetType().Assembly.Location)
        };

        return Task.FromResult((object)versionInfo);
    }

    private string GetRequiredPathSetting(string settingName)
    {
        string? configuredPath = _configuration.GetValue<string>(settingName);
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException($"Configuration value '{settingName}' is required.");
        }

        return Path.GetFullPath(configuredPath);
    }

    private bool IsUploadRequestValid(string markdown, List<IFormFile> files, out string validationMessage)
    {
        if (markdown is null)
        {
            validationMessage = "Markdown payload is required.";
            return false;
        }

        if (markdown.Length > _maxMarkdownLength)
        {
            validationMessage = $"Markdown exceeds max length of {_maxMarkdownLength} characters.";
            return false;
        }

        if (files.Count > _maxFilesPerUpload)
        {
            validationMessage = $"Request exceeds max file count of {_maxFilesPerUpload}.";
            return false;
        }

        long totalBytes = 0;
        foreach (IFormFile file in files)
        {
            string fileName = Path.GetFileName(file.FileName ?? string.Empty);
            string extension = Path.GetExtension(fileName);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                validationMessage = "File name cannot be empty.";
                return false;
            }

            if (!AcceptedUploadFormats.Contains(extension))
            {
                validationMessage = $"File extension '{extension}' is not allowed.";
                return false;
            }

            if (file.Length > _maxFileSize)
            {
                validationMessage = $"File '{fileName}' exceeds max size of {_maxFileSize} bytes.";
                return false;
            }

            totalBytes += file.Length;
            if (totalBytes > _maxTotalUploadBytes)
            {
                validationMessage = $"Total upload payload exceeds {_maxTotalUploadBytes} bytes.";
                return false;
            }
        }

        validationMessage = string.Empty;
        return true;
    }

    private async Task SaveFiles(string markdown, List<IFormFile> files, string directoryPath)
    {
        string markdownFilePath = Path.Combine(directoryPath, "content.md");
        await File.WriteAllTextAsync(markdownFilePath, markdown).ConfigureAwait(false);
        _logger.LogInformation("Markdown saved to {MarkdownFilePath}", markdownFilePath);

        long totalBytes = 0;
        foreach (IFormFile file in files)
        {
            string originalFileName = Path.GetFileName(file.FileName ?? string.Empty);
            string sanitizedFileName = Regex.Replace(originalFileName, @"[^a-zA-Z0-9().\- ]", "_");

            if (string.IsNullOrWhiteSpace(sanitizedFileName))
            {
                sanitizedFileName = "attachment.bin";
            }

            string uniqueFileName = $"{Guid.NewGuid():N}-{sanitizedFileName}";
            string filePath = Path.Combine(directoryPath, uniqueFileName);

            await using FileStream stream = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(stream).ConfigureAwait(false);

            totalBytes += file.Length;
            _logger.LogInformation("Saved uploaded file {OriginalFileName} as {StoredFileName}", originalFileName, uniqueFileName);
        }

        _logger.LogInformation("Total bytes received: {TotalBytes}", totalBytes);
    }

    private Task<FileStream?> GetFileStream(Guid identifier, string fileName, ISet<string> allowedExtensions)
    {
        if (!TryGetSafeFilePath(identifier, fileName, allowedExtensions, out string? filePath))
        {
            return Task.FromResult<FileStream?>(null);
        }

        if (!File.Exists(filePath))
        {
            return Task.FromResult<FileStream?>(null);
        }

        FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<FileStream?>(stream);
    }

    private bool TryGetSafeFilePath(Guid identifier, string fileName, ISet<string> allowedExtensions, out string? filePath)
    {
        filePath = null;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        string normalizedFileName = Path.GetFileName(fileName.Trim());
        if (!string.Equals(normalizedFileName, fileName, StringComparison.Ordinal))
        {
            return false;
        }

        if (normalizedFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        string extension = Path.GetExtension(normalizedFileName);
        if (!allowedExtensions.Contains(extension))
        {
            return false;
        }

        string noteDirectoryPath = GetNoteDirectoryPath(identifier);
        string rootPath = EnsureTrailingSeparator(Path.GetFullPath(noteDirectoryPath));
        string candidatePath = Path.GetFullPath(Path.Combine(noteDirectoryPath, normalizedFileName));

        if (!candidatePath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        filePath = candidatePath;
        return true;
    }

    private string BuildFileUrl(string actionName, Guid identifier, string fileName)
    {
        string? relativeUrl = _linkGenerator.GetPathByAction(actionName, "sharing", new { identifier, fileName });
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return string.Empty;
        }

        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null || string.IsNullOrWhiteSpace(httpContext.Request.Host.Value))
        {
            return Uri.UnescapeDataString(relativeUrl);
        }

        return Uri.UnescapeDataString($"{httpContext.Request.Scheme}://{httpContext.Request.Host}{relativeUrl}");
    }

    private static string ExtractSafeTitle(string markdownContent, Regex attachmentRegex)
    {
        string[] markdownLines = markdownContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        string noteTitle = markdownLines.Length > 0 ? markdownLines[0].Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(noteTitle) || attachmentRegex.IsMatch(noteTitle))
        {
            noteTitle = "Untitled";
        }

        return WebUtility.HtmlEncode(noteTitle);
    }

    private static string ReplaceAttachmentReference(string htmlTemplate, Regex regex, string storedFileName, string replacement)
    {
        string attachmentName = StripStoredPrefix(storedFileName);
        MatchCollection matches = regex.Matches(htmlTemplate);
        foreach (Match match in matches)
        {
            string sanitizedMatch = Regex.Replace(WebUtility.HtmlDecode(match.Value), @"[^a-zA-Z0-9().\- ]", "_");
            if (!sanitizedMatch.Contains(attachmentName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return ReplaceFirstOccurrence(htmlTemplate, match.Value, replacement);
        }

        return htmlTemplate;
    }

    private static string StripStoredPrefix(string fileName)
    {
        int separatorIndex = fileName.IndexOf('-');
        return separatorIndex >= 0 && separatorIndex + 1 < fileName.Length
            ? fileName[(separatorIndex + 1)..]
            : fileName;
    }

    private static string ReplaceFirstOccurrence(string source, string oldValue, string newValue)
    {
        int position = source.IndexOf(oldValue, StringComparison.Ordinal);
        return position < 0
            ? source
            : source[..position] + newValue + source[(position + oldValue.Length)..];
    }

    private string GetNoteDirectoryPath(Guid identifier)
    {
        return Path.Combine(_notesFolderPath, identifier.ToString("D"));
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cleanup failed for path {Path}", path);
        }
    }

    [GeneratedRegex(@"!\[\[.*?\]\]")]
    private static partial Regex FileAttachmentRegex();
}
