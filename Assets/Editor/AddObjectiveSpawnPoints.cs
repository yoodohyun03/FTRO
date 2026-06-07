using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AddObjectiveSpawnPoints
{
    static readonly string[] TargetScenePaths =
    {
        "Assets/Scenes/WesternScene.unity",
        "Assets/Scenes/CityMapScene.unity",
    };

    static readonly Vector3[] TerminalOffsets =
    {
        new(-30f, 0.1f, -30f), new(-10f, 0.1f, -30f), new(10f, 0.1f, -30f), new(30f, 0.1f, -30f),
        new(-30f, 0.1f, -10f), new(-10f, 0.1f, -10f), new(10f, 0.1f, -10f), new(30f, 0.1f, -10f),
        new(-20f, 0.1f, 20f), new(20f, 0.1f, 20f),
    };

    static readonly Vector3[] EscapeOffsets =
    {
        new(-40f, 0.1f, 40f), new(0f, 0.1f, 50f), new(40f, 0.1f, 40f),
    };

    [MenuItem("FTRO/Add Objective Spawn Points (Western + CityMap)")]
    public static void AddFromMenu()
    {
        foreach (string scenePath in TargetScenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            AddToActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SpawnPoints] Updated {scenePath}");
        }
    }

    public static void AddToActiveScene()
    {
        Transform existing = FindRoot("ObjectiveSpawnPoints");
        if (existing != null)
        {
            Debug.LogWarning("[SpawnPoints] ObjectiveSpawnPoints already exists — skipped.");
            return;
        }

        GameObject root = new GameObject("ObjectiveSpawnPoints");
        var terminalPoints = new List<Transform>();
        var escapePoints = new List<Transform>();

        for (int i = 0; i < TerminalOffsets.Length; i++)
        {
            GameObject point = new GameObject($"TerminalSpawn_{i}");
            point.transform.SetParent(root.transform, false);
            point.transform.localPosition = TerminalOffsets[i];
            terminalPoints.Add(point.transform);
        }

        for (int i = 0; i < EscapeOffsets.Length; i++)
        {
            GameObject point = new GameObject($"EscapeSpawn_{i}");
            point.transform.SetParent(root.transform, false);
            point.transform.localPosition = EscapeOffsets[i];
            escapePoints.Add(point.transform);
        }

        GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("[SpawnPoints] GameManager not found.");
            return;
        }

        SerializedObject serialized = new SerializedObject(gameManager);
        serialized.FindProperty("terminalSpawnPoints").ClearArray();
        for (int i = 0; i < terminalPoints.Count; i++)
        {
            serialized.FindProperty("terminalSpawnPoints").InsertArrayElementAtIndex(i);
            serialized.FindProperty("terminalSpawnPoints").GetArrayElementAtIndex(i).objectReferenceValue =
                terminalPoints[i];
        }

        serialized.FindProperty("escapeSpawnPoints").ClearArray();
        for (int i = 0; i < escapePoints.Count; i++)
        {
            serialized.FindProperty("escapeSpawnPoints").InsertArrayElementAtIndex(i);
            serialized.FindProperty("escapeSpawnPoints").GetArrayElementAtIndex(i).objectReferenceValue =
                escapePoints[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gameManager);
    }

    static Transform FindRoot(string exactName)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == exactName)
                return root.transform;
        }

        return null;
    }
}
