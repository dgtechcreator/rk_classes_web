namespace SchoolMS.Web.Auth;

/// <summary>
/// Stores class-note files OUTSIDE wwwroot (App_Data/class-notes) so they can only be fetched through the notes API, which checks
/// that the caller is staff with the right or a parent / student of that class. The type is decided from the file's own bytes.
/// </summary>
public class NoteFileStore(IWebHostEnvironment env)
{
    public const long MaxBytes = 15 * 1024 * 1024;

    static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf", [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".ppt"] = "application/vnd.ms-powerpoint", [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".xls"] = "application/vnd.ms-excel", [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".txt"] = "text/plain",
    };

    string Dir => Path.Combine(env.ContentRootPath, "App_Data", "class-notes");

    public static string ContentType(string fileName) => Types.TryGetValue(Path.GetExtension(fileName), out var t) ? t : "application/octet-stream";

    /// <summary>Saves the upload. Returns the stored file name (a GUID + extension) or an error message.</summary>
    public (string? stored, string? error) Save(IFormFile? file)
    {
        if (file == null || file.Length == 0) return (null, "Please choose a file.");
        if (file.Length > MaxBytes) return (null, "File is too large (max 15 MB).");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Types.ContainsKey(ext)) return (null, "Allowed files: PDF, Word, PowerPoint, Excel, JPG, PNG or TXT.");

        var head = new byte[8];
        int n;
        using (var s = file.OpenReadStream()) n = s.Read(head, 0, head.Length);
        if (!SignatureOk(ext, head, n)) return (null, "This file does not look like a real " + ext.TrimStart('.').ToUpperInvariant() + " file.");

        Directory.CreateDirectory(Dir);
        var stored = $"{Guid.NewGuid():N}{ext}";
        using (var fs = new FileStream(Path.Combine(Dir, stored), FileMode.CreateNew)) file.CopyTo(fs);
        return (stored, null);
    }

    static bool SignatureOk(string ext, byte[] b, int n) => ext switch
    {
        ".pdf" => n >= 4 && b[0] == '%' && b[1] == 'P' && b[2] == 'D' && b[3] == 'F',
        ".jpg" or ".jpeg" => n >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
        ".png" => n >= 4 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47,
        ".docx" or ".pptx" or ".xlsx" => n >= 2 && b[0] == 'P' && b[1] == 'K',
        ".doc" or ".ppt" or ".xls" => n >= 4 && b[0] == 0xD0 && b[1] == 0xCF && b[2] == 0x11 && b[3] == 0xE0,
        _ => true,   // txt
    };

    /// <summary>Opens a stored file for download; null when it is missing or the name is not one this store wrote.</summary>
    public FileStream? Open(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored) || stored.Contains('/') || stored.Contains('\\') || stored.Contains("..")) return null;
        var full = Path.Combine(Dir, stored);
        return File.Exists(full) ? new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }
}
