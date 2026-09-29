using System.Text.Json;
using TaskManager.Services;

namespace TaskManager_WinUI.Services;

public sealed class UserSettings
{
    public string Theme { get; set; }="System";
}

public sealed class UserSettingsService
{
    private readonly string path;
    public UserSettings Current { get; private set; }
    public UserSettingsService()
    {
        path=Path.Combine(new TaskStore().DirectoryPath,"settings.json");
        try{Current=File.Exists(path)?JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path))??new():new();}catch{Current=new();}
    }
    public void Save(){Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(Current,new JsonSerializerOptions{WriteIndented=true}));}
}
