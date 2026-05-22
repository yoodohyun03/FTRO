using UnityEngine;
using UnityEditor;
using System.IO;

public class MovePrefabs : Editor
{
    [MenuItem("Tools/Move Prefabs to Resources")]
    public static void Execute()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        MoveAsset("Assets/Models/HackingTerminal.prefab", "Assets/Resources/HackingTerminal.prefab");
        MoveAsset("Assets/Models/EscapeZonePad.prefab", "Assets/Resources/EscapeZonePad.prefab");
    }

    private static void MoveAsset(string oldPath, string newPath)
    {
        if (File.Exists(oldPath))
        {
            string error = AssetDatabase.MoveAsset(oldPath, newPath);
            if (!string.IsNullOrEmpty(error)) Debug.LogError(error);
        }
    }
}
