using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the Main Menu scene.
/// Menu: Paint Game → Setup Main Menu
/// </summary>
public class MainMenuSetup : EditorWindow
{
    [MenuItem("Paint Game/Setup Main Menu")]
    public static void ShowWindow() => GetWindow<MainMenuSetup>("Main Menu Setup");

    void OnGUI()
    {
        GUILayout.Label("Main Menu Scene Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Creates all Main Menu UI in the current scene.", MessageType.Info);
        EditorGUILayout.Space();
        if (GUILayout.Button("Create Main Menu", GUILayout.Height(40))) CreateScene();
    }

    void CreateScene()
    {
        // EventSystem
        if (FindObjectOfType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        // Camera
        Camera cam = Camera.main;
        if (cam == null)
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.14f);
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0, 0, -10);

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Gradient background panel
        var bgPanel = MakePanel("Background", canvasGO.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bgPanel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.16f);

        // Decorative top stripe
        var topStripe = MakePanel("TopStripe", canvasGO.transform,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -8f), Vector2.zero);
        topStripe.GetComponent<Image>().color = new Color(0.55f, 0.35f, 0.95f);

        // Decorative bottom stripe
        var botStripe = MakePanel("BottomStripe", canvasGO.transform,
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(0, 8f), Vector2.zero);
        botStripe.GetComponent<Image>().color = new Color(0.35f, 0.75f, 0.95f);

        // Title
        var titleGO = MakeText("Title", canvasGO.transform,
            new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.88f),
            "🎨 Paint World", 72, new Color(1f, 0.95f, 0.6f));

        // Subtitle
        MakeText("Subtitle", canvasGO.transform,
            new Vector2(0.2f, 0.44f), new Vector2(0.8f, 0.58f),
            "Tô màu thế giới của bạn", 22, new Color(0.75f, 0.75f, 0.9f));

        // Start Button
        var startBtn = MakeButton("StartButton", canvasGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -30f), new Vector2(300f, 70f),
            "▶  Bắt đầu chơi", new Color(0.35f, 0.75f, 0.45f), 26);

        // Quit Button
        var quitBtn = MakeButton("QuitButton", canvasGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -115f), new Vector2(200f, 50f),
            "Thoát", new Color(0.4f, 0.15f, 0.15f), 18);

        // MainMenuManager
        var mgrGO = new GameObject("MainMenuManager");
        var mgr = mgrGO.AddComponent<MainMenuManager>();
        mgr.startButton = startBtn.GetComponent<Button>();
        mgr.quitButton  = quitBtn.GetComponent<Button>();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Done!", "Main Menu created!\nSave scene as 'MainMenu' (Ctrl+S)", "OK");
    }

    // ── Helpers ───────────────────────────────────────────────────

    GameObject MakePanel(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.sizeDelta = sizeDelta; rt.anchoredPosition = anchoredPos;
        go.AddComponent<Image>();
        return go;
    }

    Text MakeText(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        string content, int fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var txt = go.AddComponent<Text>();
        txt.text = content; txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = fontSize; txt.color = color;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return txt;
    }

    GameObject MakeButton(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta,
        string label, Color bgColor, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta; rt.anchoredPosition = anchoredPos;
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var tRT = txtGO.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;
        var txt = txtGO.AddComponent<Text>();
        txt.text = label; txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = fontSize; txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return go;
    }
}
