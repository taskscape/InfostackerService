namespace ShareAPI.Services;

public interface ISharingService
{
    Task<Guid> UploadMarkdownWithFiles(string markdown, List<IFormFile> files);
    Task<string> GetMarkdownContent(string identifier);
    Task<bool> UpdateMarkdownWithFiles(string markdown, List<IFormFile> files, Guid identifier);
    Task<bool> DeleteMarkdownWithFiles(string identifier);
    Task<FileStream> GetPdf(string identifier, string fileName);
    Task<FileStream> GetDoc(string identifier, string fileName);
    Task<FileStream> GetImage(string identifier, string fileName);
    Task<FileStream> GetVideo(string identifier, string fileName);
    Task<object> GetVersion();
}