namespace SchoolMS.Web.Auth;

/// <summary>
/// One place that stores profile photos for students, faculty and users, shared by the web forms and the mobile API so
/// both write the same files and the same /images/{folder}/{guid}.ext paths in the DB. The file type is decided from the
/// file's own bytes (never from the client's file name), so only real JPEG / PNG / WEBP images can ever be saved.
/// </summary>
public class ProfilePhotoStore(IWebHostEnvironment env)
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public static readonly string[] Folders = { "students", "faculty", "users" };

    /// <summary>Saves the upload under wwwroot/images/{folder}. Returns the web path to store in the DB, or an error message.</summary>
    public (string? path, string? error) Save(IFormFile? file, string folder)
    {
        if (file == null || file.Length == 0) return (null, "Please choose a photo.");
        if (file.Length > MaxBytes) return (null, "Photo is too large (max 5 MB).");
        if (!Folders.Contains(folder)) return (null, "Invalid photo folder.");

        string? ext;
        using (var s = file.OpenReadStream())
        {
            var head = new byte[12];
            var n = s.Read(head, 0, head.Length);
            ext = ExtensionFor(head, n);
        }
        if (ext == null) return (null, "Only JPG, PNG or WEBP photos are allowed.");

        var dir = Path.Combine(env.WebRootPath, "images", folder);
        Directory.CreateDirectory(dir);
        var fn = $"{Guid.NewGuid():N}{ext}";
        using (var fs = new FileStream(Path.Combine(dir, fn), FileMode.CreateNew))
            file.CopyTo(fs);
        return ($"/images/{folder}/{fn}", null);
    }

    /// <summary>Deletes a previously stored photo file. Ignores anything that is not a photo this store wrote.</summary>
    public void Delete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        foreach (var folder in Folders)
        {
            var prefix = $"/images/{folder}/";
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var name = path.Substring(prefix.Length);
            if (name.Length == 0 || name.Contains('/') || name.Contains('\\') || name.Contains("..")) return;
            try
            {
                var full = Path.Combine(env.WebRootPath, "images", folder, name);
                if (File.Exists(full)) File.Delete(full);
            }
            catch { /* a locked / missing file must never fail the request */ }
            return;
        }
    }

    static string? ExtensionFor(byte[] b, int n)
    {
        if (n >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
        if (n >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ".png";
        if (n >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return ".webp";
        return null;
    }
}
