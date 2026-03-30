using System.IO;
using UnityEngine;

public static class ReplayFileStore
{
    public static void Save(string absolutePath, ReplayData replay)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || replay == null)
            return;

        string directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        string json = JsonUtility.ToJson(replay, true);
        File.WriteAllText(absolutePath, json);
    }

    public static ReplayData Load(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            return null;

        string json = File.ReadAllText(absolutePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonUtility.FromJson<ReplayData>(json);
    }
}
