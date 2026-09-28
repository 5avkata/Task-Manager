using System.Text.Json;
using System.Text.Json.Serialization;
using TaskManager.Models;

namespace TaskManager.Services;

public sealed record LoadResult(AppData Data, string? Warning = null, bool ReadOnly = false);

public sealed class TaskStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public string DirectoryPath { get; }
    public string FilePath => Path.Combine(DirectoryPath, "tasks.json");
    public string BackupPath => Path.Combine(DirectoryPath, "tasks.backup.json");

    public TaskStore(string? directory = null) => DirectoryPath = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SchoolPortfolio", "TaskManager");

    public LoadResult Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                if (File.Exists(BackupPath)) return new(Read(BackupPath), "The main data file was missing. Your last backup has been recovered.");
                return new(new AppData());
            }
            return new(Read(FilePath));
        }
        catch (NotSupportedException ex) { return new(new AppData(), ex.Message, true); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            // Preserve the original before allowing the user to save a fresh collection.
            try
            {
                if (File.Exists(FilePath))
                    File.Move(FilePath, Path.Combine(DirectoryPath, $"tasks.unreadable-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json"));
            }
            catch (Exception preserveError) when (preserveError is IOException or UnauthorizedAccessException)
            {
                return new(new AppData(), "Your data could not be opened or safely preserved. Changes are disabled for this session.\n\n" + preserveError.Message, true);
            }
            try
            {
                if (File.Exists(BackupPath))
                    return new(Read(BackupPath), "The data file could not be read. It has been preserved, and the last backup was recovered. Your most recent change may be missing.");
            }
            catch (Exception backupError) when (backupError is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException) { }
            return new(new AppData(), "Saved data could not be read. Any unreadable main file has been preserved in the data folder. A new task list is ready.\n\n" + ex.Message);
        }
    }

    private static AppData Read(string path)
    {
        var data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("The data file is empty.");
        if (data.Version != 1) throw new NotSupportedException("This data file uses an unsupported version. Changes are disabled to protect it.");
        if (data.Tasks is null || data.Categories is null) throw new InvalidDataException("The data file has missing collections.");
        var categories = new List<string> { "Personal", "School", "Work" };
        foreach (var category in data.Categories)
            if (!string.IsNullOrWhiteSpace(category) && !categories.Contains(category.Trim(), StringComparer.OrdinalIgnoreCase)) categories.Add(category.Trim());
        var ids = new HashSet<Guid>();
        foreach (var task in data.Tasks)
        {
            if (task is null || string.IsNullOrWhiteSpace(task.Title) || !Enum.IsDefined(task.Priority)
                || task.DueDate.Year < 1753 || task.DueDate.Year > 9998)
                throw new InvalidDataException("A saved task contains invalid fields.");
            task.Title = task.Title.Trim();
            task.Description ??= "";
            task.Category = string.IsNullOrWhiteSpace(task.Category) ? "Personal" : task.Category.Trim();
            var canonical = categories.FirstOrDefault(c => c.Equals(task.Category, StringComparison.OrdinalIgnoreCase));
            if (canonical is null) categories.Add(task.Category); else task.Category = canonical;
            if (task.Id == Guid.Empty || !ids.Add(task.Id)) { task.Id = Guid.NewGuid(); ids.Add(task.Id); }
            task.DueDate = task.DueDate.Date;
        }
        data.Categories = categories;
        return data;
    }

    public void Save(AppData data)
    {
        Directory.CreateDirectory(DirectoryPath);
        string temporaryPath = Path.Combine(DirectoryPath, $"tasks-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, data, Options);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(FilePath)) File.Replace(temporaryPath, FilePath, BackupPath);
            else File.Move(temporaryPath, FilePath);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
