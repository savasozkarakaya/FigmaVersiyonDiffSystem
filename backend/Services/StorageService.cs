namespace FigmaDiffBackend.Services;

public interface IStorageService
{
    Task<string> SaveImageAsync(Stream imageStream, string prefix);
    string GetImagePath(string relativePath);
    Task<byte[]> GetImageBytesAsync(string relativePath);
}

public class LocalStorageService : IStorageService
{
    private readonly string _rootPath;

    public LocalStorageService(IWebHostEnvironment env)
    {
        _rootPath = Path.Combine(env.ContentRootPath, "storage");
        if (!Directory.Exists(_rootPath))
        {
            Directory.CreateDirectory(_rootPath);
        }
    }

    public async Task<string> SaveImageAsync(Stream imageStream, string prefix)
    {
        var fileName = $"{prefix}_{Guid.NewGuid()}.png";
        var fullPath = Path.Combine(_rootPath, fileName);
        
        using var fs = new FileStream(fullPath, FileMode.Create);
        await imageStream.CopyToAsync(fs);
        
        return fileName; // Return relative path (just filename for now)
    }

    public string GetImagePath(string relativePath)
    {
        return Path.Combine(_rootPath, relativePath);
    }
    
    public async Task<byte[]> GetImageBytesAsync(string relativePath)
    {
        var path = GetImagePath(relativePath);
        if (!File.Exists(path)) return Array.Empty<byte>();
        return await File.ReadAllBytesAsync(path);
    }
}
