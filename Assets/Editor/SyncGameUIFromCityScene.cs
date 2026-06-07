using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SyncGameUIFromCityScene
{
    const string SourceScenePath = "Assets/Scenes/CityScene.unity";
    const string MinimapPrefabPath = "Assets/Prefabs/MinimapSystem.prefab";

    static readonly string[] TargetScenePaths =
    {
        "Assets/Scenes/WesternScene.unity",
        "Assets/Scenes/CityMapScene.unity",
    };

    [MenuItem("FTRO/Sync Game UI From CityScene")]
    public static void SyncFromMenu()
    {
        Sync();
    }

    public static void Sync()
    {
        var sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);

        GameObject sourceCanvas = FindSceneRoot("Canvas");
        GameObject sourceChatManager = FindSceneRoot("ChatManager");
        GameObject sourceEventSystem = FindSceneRoot("EventSystem");
        GameObject sourceMinimap = FindSceneRoot("MinimapSystem");

        if (sourceCanvas == null)
        {
            Debug.LogError("[UI Sync] CityScene Canvas not found.");
            return;
        }

        var stash = new GameObject("__UI_SYNC_STASH__");
        Object.DontDestroyOnLoad(stash);

        GameObject canvasTemplate = CloneForStash(sourceCanvas, "Canvas", stash.transform);
        GameObject chatTemplate = sourceChatManager != null
            ? CloneForStash(sourceChatManager, "ChatManager", stash.transform)
            : null;
        GameObject eventTemplate = sourceEventSystem != null
            ? CloneForStash(sourceEventSystem, "EventSystem", stash.transform)
            : null;
        GameObject minimapTemplate = sourceMinimap != null
            ? CloneForStash(sourceMinimap, "MinimapSystem", stash.transform)
            : null;

        foreach (string targetPath in TargetScenePaths)
        {
            Scene targetScene = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Single);
            RemoveExistingGameUI();

            GameObject canvas = PlaceInScene(canvasTemplate, targetScene);
            NormalizeCanvas(canvas);

            if (chatTemplate != null)
            {
                GameObject chatManager = PlaceInScene(chatTemplate, targetScene);
                WireChatManager(chatManager, canvas);
            }

            if (eventTemplate != null)
                PlaceInScene(eventTemplate, targetScene);
            else
                EnsureEventSystem(targetScene);

            if (minimapTemplate != null)
                PlaceInScene(minimapTemplate, targetScene);
            else
                InstantiateMinimapPrefab(targetScene);

            WireGameManager(canvas);
            WireExitButton(canvas);
            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorSceneManager.SaveScene(targetScene);
            Debug.Log($"[UI Sync] Applied CityScene UI to {targetPath}");
        }

        Object.DestroyImmediate(stash);
        EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        Debug.Log("[UI Sync] Complete.");
    }

    static GameObject CloneForStash(GameObject source, string name, Transform parent)
    {
        GameObject copy = Object.Instantiate(source);
        copy.name = name;
        copy.transform.SetParent(parent, false);
        copy.SetActive(false);
        return copy;
    }

    static GameObject PlaceInScene(GameObject template, Scene scene)
    {
        GameObject instance = Object.Instantiate(template);
        instance.name = template.name;
        instance.SetActive(true);
        SceneManager.MoveGameObjectToScene(instance, scene);
        return instance;
    }

    static GameObject FindSceneRoot(string exactName)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == exactName)
                return root;
        }

        return null;
    }

    static void RemoveExistingGameUI()
    {
        List<GameObject> roots = SceneManager.GetActiveScene().GetRootGameObjects().ToList();
        foreach (GameObject root in roots)
        {
            if (ShouldRemoveRoot(root.name))
                Object.DestroyImmediate(root);
        }
    }

    static bool ShouldRemoveRoot(string name)
    {
        if (name == "Canvas" || name.StartsWith("Canvas "))
            return true;
        if (name == "ChatManager" || name.StartsWith("ChatManager"))
            return true;
        if (name == "EventSystem" || name.StartsWith("EventSystem"))
            return true;
        if (name == "MinimapSystem")
            return true;
        return false;
    }

    static void NormalizeCanvas(GameObject canvas)
    {
        Transform corner = canvas.transform.Find("CornerRoleText");
        if (corner != null)
            corner.gameObject.SetActive(false);

        Transform chatPanel = canvas.transform.Find("ChatPanel");
        if (chatPanel == null)
            return;

        RectTransform panelRect = chatPanel.GetComponent<RectTransform>();
        panelRect.localScale = Vector3.one;
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-20f, 20f);
        panelRect.sizeDelta = new Vector2(420f, 300f);

        Transform chatLog = chatPanel.Find("ChatLogText");
        if (chatLog != null)
        {
            RectTransform logRect = chatLog.GetComponent<RectTransform>();
            logRect.localScale = Vector3.one;
            logRect.anchorMin = Vector2.zero;
            logRect.anchorMax = Vector2.one;
            logRect.pivot = new Vector2(0.5f, 0.5f);
            logRect.offsetMin = new Vector2(10f, 64f);
            logRect.offsetMax = new Vector2(-10f, -8f);

            TextMeshProUGUI logText = chatLog.GetComponent<TextMeshProUGUI>();
            if (logText != null)
            {
                logText.fontSize = 30f;
                logText.enableAutoSizing = false;
            }
        }

        Transform chatInput = chatPanel.Find("ChatInputField");
        if (chatInput != null)
        {
            RectTransform inputRect = chatInput.GetComponent<RectTransform>();
            inputRect.localScale = Vector3.one;
            inputRect.anchorMin = new Vector2(0f, 0f);
            inputRect.anchorMax = new Vector2(1f, 0f);
            inputRect.pivot = new Vector2(0.5f, 0f);
            inputRect.anchoredPosition = new Vector2(0f, 24f);
            inputRect.sizeDelta = new Vector2(-24f, 48f);

            TMP_InputField inputField = chatInput.GetComponent<TMP_InputField>();
            if (inputField != null)
            {
                if (inputField.textComponent != null)
                {
                    inputField.textComponent.fontSize = 26f;
                    inputField.textComponent.enableAutoSizing = false;
                }

                if (inputField.placeholder is TMP_Text placeholder)
                {
                    placeholder.fontSize = 26f;
                    placeholder.enableAutoSizing = false;
                }
            }
        }
    }

    static void WireGameManager(GameObject canvas)
    {
        GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogWarning("[UI Sync] GameManager not found in target scene.");
            return;
        }

        SerializedObject serialized = new SerializedObject(gameManager);
        AssignIfFound(serialized, "gameOverPanel", canvas.transform, "GameOverPanel");
        AssignIfFound(serialized, "centerText", canvas.transform, "CenterAnnounceText", true);
        AssignIfFound(serialized, "timerText", canvas.transform, "TimerText", true);
        AssignIfFound(serialized, "objectiveStatusText", canvas.transform, "ObjectiveStatusText", true);
        serialized.FindProperty("roleIndicatorText").objectReferenceValue = null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gameManager);
    }

    static void WireExitButton(GameObject canvas)
    {
        GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
        if (gameManager == null)
            return;

        Transform exitButton = canvas.transform.Find("ExitButton");
        if (exitButton == null)
            return;

        Button button = exitButton.GetComponent<Button>();
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(gameManager.OnClickExit);
        EditorUtility.SetDirty(button);
    }

    static void WireChatManager(GameObject chatManagerObject, GameObject canvas)
    {
        ChatManager chatManager = chatManagerObject.GetComponent<ChatManager>();
        if (chatManager == null)
            return;

        Transform chatPanel = canvas.transform.Find("ChatPanel");
        if (chatPanel == null)
        {
            Debug.LogWarning("[UI Sync] ChatPanel not found under Canvas.");
            return;
        }

        TMP_InputField chatInput = chatPanel.Find("ChatInputField")?.GetComponent<TMP_InputField>();
        TextMeshProUGUI chatLog = chatPanel.Find("ChatLogText")?.GetComponent<TextMeshProUGUI>();

        SerializedObject serialized = new SerializedObject(chatManager);
        serialized.FindProperty("chatInput").objectReferenceValue = chatInput;
        serialized.FindProperty("chatLog").objectReferenceValue = chatLog;
        serialized.FindProperty("isLobbyScene").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(chatManager);
    }

    static void AssignIfFound(
        SerializedObject serialized,
        string propertyName,
        Transform canvas,
        string childName,
        bool tmpText = false)
    {
        Transform child = canvas.Find(childName);
        if (child == null)
            return;

        Object value = tmpText ? child.GetComponent<TextMeshProUGUI>() : child.gameObject;
        if (value == null)
            return;

        serialized.FindProperty(propertyName).objectReferenceValue = value;
    }

    static void EnsureEventSystem(Scene scene)
    {
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        SceneManager.MoveGameObjectToScene(eventSystem, scene);
    }

    static void InstantiateMinimapPrefab(Scene scene)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MinimapPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[UI Sync] MinimapSystem prefab not found.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "MinimapSystem";
    }
}
