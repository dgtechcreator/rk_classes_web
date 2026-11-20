namespace SchoolMS.Web.Auth;

/// <summary>
/// Where uploaded files live. A new deploy replaces the website folder (wwwroot, App_Data ...), which used to delete every
/// profile photo and class note. So uploads go to a folder OUTSIDE the deployed site that a deploy never touches:
///   1. <c>Uploads:Root</c> from appsettings / environment (<c>Uploads__Root</c>), if set;
///   2. otherwise (not Development) a sibling folder of the website folder: <c>..\RKClasses_Uploads</c>;
///   3. if that folder cannot be created / written, the old place inside the site (nothing breaks, files just are not deploy-safe).
/// The DB paths do not change (<c>/images/{folder}/{file}</c>); a static-file mapping serves them from the new folder, and
/// files still sitting in the old wwwroot folder keep being served too. Old files are copied across once at startup.
/// </summary>
public class UploadLocations
{
    readonly IWebHostEnvironment env;

    /// <summary>Folder outside the site that holds uploads, or null = the old in-site folders.</summary>
    public string? Root { get; }

    /// <summary>Why this location was chosen (written to the log at startup).</summary>
    public string Description { get; }

    public UploadLocations(IWebHostEnvironment env, IConfiguration cfg)
    {
        this.env = env;
        var contentRoot = env.ContentRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        string? candidate = null;
        string how;
        var configured = cfg["Uploads:Root"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            candidate = Path.GetFullPath(configured.Trim(), contentRoot);
            how = "Uploads:Root setting";
        }
        else if (!env.IsDevelopment() && Directory.GetParent(contentRoot) is { } parent)
        {
            candidate = Path.Combine(parent.FullName, "RKClasses_Uploads");
            how = "folder next to the website";
        }
        else how = "development default";

        if (candidate != null && CanWrite(candidate))
        {
            Root = candidate;
            Description = $"Uploads are stored in {candidate} ({how}) - safe across deploys.";
            foreach (var f in ProfilePhotoStore.Folders) Directory.CreateDirectory(Path.Combine(candidate, "photos", f));
            Directory.CreateDirectory(Path.Combine(candidate, "class-notes"));
        }
        else
        {
            Description = candidate == null
                ? "Uploads stay inside the website folder (development default)."
                : $"WARNING: cannot write to {candidate}; uploads stay inside the website folder and a deploy can delete them.";
        }
    }

    /// <summary>Photos folder served at /images (contains students / faculty / users), or null when not using the outside folder.</summary>
    public string? PhotosRoot => Root == null ? null : Path.Combine(Root, "photos");

    /// <summary>Where NEW photos of this kind are written.</summary>
    public string PhotoDir(string folder) => Root != null ? Path.Combine(Root, "photos", folder) : LegacyPhotoDir(folder);
    public string LegacyPhotoDir(string folder) => Path.Combine(env.WebRootPath, "images", folder);

    /// <summary>Where NEW class-note files are written.</summary>
    public string NotesDir => Root != null ? Path.Combine(Root, "class-notes") : LegacyNotesDir;
    public string LegacyNotesDir => Path.Combine(env.ContentRootPath, "App_Data", "class-notes");

    /// <summary>One-time, never overwrites: copies files that still sit in the old in-site folders into the outside folder.</summary>
    public int MigrateLegacy()
    {
        if (Root == null) return 0;
        int copied = 0;
        foreach (var f in ProfilePhotoStore.Folders) copied += CopyMissing(LegacyPhotoDir(f), PhotoDir(f));
        copied += CopyMissing(LegacyNotesDir, NotesDir);
        return copied;
    }

    static int CopyMissing(string from, string to)
    {
        if (!Directory.Exists(from)) return 0;
        int n = 0;
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from))
        {
            var dest = Path.Combine(to, Path.GetFileName(file));
            if (File.Exists(dest)) continue;
            try { File.Copy(file, dest); n++; } catch { /* a locked file is retried on the next start */ }
        }
        return n;
    }

    static bool CanWrite(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }
}
