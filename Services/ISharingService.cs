namespace Infostacker.Services;

public interface ISharingService
{
    Task<Guid> UploadMarkdownWithFiles(string markdown, List<IFormFile> files);
    Task<string?> GetMarkdownContent(Guid identifier);
    Task<bool> UpdateMarkdownWithFiles(string markdown, List<IFormFile> files, Guid identifier);
    Task<bool> DeleteMarkdownWithFiles(Guid identifier);
    Task<FileStream?> GetPdf(Guid identifier, string fileName);
    Task<FileStream?> GetDoc(Guid identifier, string fileName);
    Task<FileStream?> GetImage(Guid identifier, string fileName);
    Task<FileStream?> GetVideo(Guid identifier, string fileName);
    Task<object> GetVersion();
}
