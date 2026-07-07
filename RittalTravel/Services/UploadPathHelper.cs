namespace RittalTravel.Services;

public static class UploadPathHelper
{
    public static string GetUploadsDirectory(IWebHostEnvironment env)
        => Path.Combine(env.ContentRootPath, "Uploads");

    public static string? ResolveUploadFilePath(IWebHostEnvironment env, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(safeFileName)) return null;

        var uploadsDir = Path.GetFullPath(GetUploadsDirectory(env));
        var filePath = Path.GetFullPath(Path.Combine(uploadsDir, safeFileName));

        if (!filePath.StartsWith(uploadsDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !filePath.Equals(uploadsDir, StringComparison.OrdinalIgnoreCase))
            return null;

        return filePath;
    }

    public static bool IsValidStoredFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        var safe = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(safe) || safe != fileName) return false;
        var ext = Path.GetExtension(safe).ToLowerInvariant();
        return ext is ".pdf" or ".jpg" or ".jpeg" or ".png"
               && Guid.TryParse(Path.GetFileNameWithoutExtension(safe), out _);
    }

    public static bool TryDeleteUploadFile(IWebHostEnvironment env, string? fileName)
    {
        var filePath = ResolveUploadFilePath(env, fileName);
        if (filePath == null || !File.Exists(filePath)) return false;
        File.Delete(filePath);
        return true;
    }
}
