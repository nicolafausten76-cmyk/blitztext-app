using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
namespace Sprechklar;

public sealed class Settings
{
    public string Language { get; set; } = "de";
    public string Model { get; set; } = "base";
    public string Dictionary { get; set; } = "n acht n => n8n";
    public string OllamaModel { get; set; } = "";
    public int Device { get; set; }
    public bool SaveHistory { get; set; }
}
public record HistoryItem(DateTimeOffset At, string Mode, string Text);
public static class Storage
{
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sprechklar");
    public static string ModelPath(string name) => Path.Combine(Root, "models", "ggml-" + (name == "tiny" ? "tiny" : "base") + ".bin");
    private static string SettingsPath => Path.Combine(Root, "settings.json");
    private static string HistoryPath => Path.Combine(Root, "history.dpapi");
    public static Settings Load()
    {
        Directory.CreateDirectory(Root);
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new(); }
        catch (Exception e) when (e is IOException || e is JsonException) { return new(); }
    }
    public static void Save(Settings settings)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(SettingsPath + ".tmp", JsonSerializer.Serialize(settings));
        File.Move(SettingsPath + ".tmp", SettingsPath, true);
    }
    public static List<HistoryItem> History()
    {
        if (!File.Exists(HistoryPath)) return new();
        byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(HistoryPath), null, DataProtectionScope.CurrentUser);
        try { return JsonSerializer.Deserialize<List<HistoryItem>>(plain) ?? new(); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public static void AddHistory(HistoryItem item)
    {
        var items = History(); items.Insert(0, item);
        if (items.Count > 100) items.RemoveRange(100, items.Count - 100);
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(items);
        try { File.WriteAllBytes(HistoryPath + ".tmp", ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        File.Move(HistoryPath + ".tmp", HistoryPath, true);
    }
    public static void ClearHistory() { if (File.Exists(HistoryPath)) File.Delete(HistoryPath); }
}
