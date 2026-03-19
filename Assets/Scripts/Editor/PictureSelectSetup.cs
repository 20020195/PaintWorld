using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Builds the Picture Select scene with a horizontal carousel.
/// Menu: Paint Game → Setup Picture Select
/// </summary>
public class PictureSelectSetup : EditorWindow
{
    [MenuItem("Paint Game/Setup Picture Select")]
    public static void ShowWindow() => GetWindow<PictureSelectSetup>("Picture Select Setup");

    void OnGUI()
    {
        GUILayout.Label("Picture Select Scene Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "This will create the Picture Select scene.\n" +
            "Cards will be dynamically loaded at runtime from any 'PictureData' ScriptableObjects found in the project.",
            MessageType.Info);
            
        EditorGUILayout.Space();

        if (GUILayout.Button("Create Picture Select Scene", GUILayout.Height(40))) CreateScene();
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

        // Background
        var bgPanel = MakePanel("Background", canvasGO.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bgPanel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.16f);

        // Top Bar
        var topBar = MakePanel("TopBar", canvasGO.transform,
            new Vector2(0,1), new Vector2(1,1), new Vector2(0, -54f), Vector2.zero);
        topBar.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.20f, 0.97f);

        // Back button
        var backBtn = MakeButton("BackButton", topBar.transform,
            new Vector2(0,0), new Vector2(0,1),
            new Vector2(60f, 0), new Vector2(110f, 0),
            "← Menu", new Color(0.28f, 0.28f, 0.38f), 16);

        // Title in top bar
        MakeText("TopTitle", topBar.transform,
            new Vector2(0.3f, 0), new Vector2(0.7f, 1),
            "Chọn tranh", 24, Color.white);

        // Section title
        MakeText("SectionTitle", canvasGO.transform,
            new Vector2(0.2f, 0.82f), new Vector2(0.8f, 0.94f),
            "Chọn bức tranh bạn muốn tô", 20, new Color(0.7f, 0.7f, 0.85f));

        // ── Carousel viewport ────────────────────────────────────
        var viewport = MakePanel("CarouselViewport", canvasGO.transform,
            new Vector2(0, 0.12f), new Vector2(1, 0.82f),
            Vector2.zero, Vector2.zero);
        viewport.GetComponent<Image>().color = new Color(0,0,0,0);
        viewport.AddComponent<RectMask2D>();
        var carouselDragProxy = viewport.AddComponent<CarouselDragProxy>();

        // Content container (moves left/right)
        var contentGO = new GameObject("CarouselContent");
        contentGO.transform.SetParent(viewport.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.5f, 0);
        contentRT.anchorMax = new Vector2(0.5f, 1);
        contentRT.sizeDelta = new Vector2(0, 0);
        contentRT.anchoredPosition = Vector2.zero;

        // ── Bottom bar: Selected name + Choose button ─────────────
        var bottomBar = MakePanel("BottomBar", canvasGO.transform,
            new Vector2(0, 0), new Vector2(1, 0.13f),
            Vector2.zero, Vector2.zero);
        bottomBar.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.18f, 0.97f);

        var selectedNameText = MakeText("SelectedNameText", bottomBar.transform,
            new Vector2(0.05f, 0.45f), new Vector2(0.60f, 0.95f),
            "", 22, new Color(1f, 0.9f, 0.5f));

        var chooseBtn = MakeButton("ChooseButton", bottomBar.transform,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-100f, 0), new Vector2(170f, 52f),
            "✔ Chọn tranh này", new Color(0.25f, 0.72f, 0.42f), 17);

        // Left/Right nav arrows
        var leftArrow = MakeButton("LeftArrow", canvasGO.transform,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(30f, 0), new Vector2(50f, 80f),
            "‹", new Color(0.3f, 0.3f, 0.45f, 0.8f), 40);

        var rightArrow = MakeButton("RightArrow", canvasGO.transform,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-30f, 0), new Vector2(50f, 80f),
            "›", new Color(0.3f, 0.3f, 0.45f, 0.8f), 40);

        // ── PictureCarousel ──────────────────────────────────────
        var carouselGO = new GameObject("PictureCarousel");
        carouselGO.transform.SetParent(canvasGO.transform, false);
        var carousel = carouselGO.AddComponent<PictureCarousel>();
        carousel.contentContainer  = contentRT;
        carousel.selectButton      = chooseBtn.GetComponent<Button>();
        carousel.selectedNameText  = selectedNameText;
        carousel.backButton        = backBtn.GetComponent<Button>();
        carousel.leftArrowButton   = leftArrow.GetComponent<Button>();
        carousel.rightArrowButton  = rightArrow.GetComponent<Button>();
        carousel.cardSpacing       = 520f;
        carousel.paintSceneName    = "PaintScene";

        carouselDragProxy.carousel = carousel;

        // Find and attach all PictureData scriptable objects
        string[] guids = AssetDatabase.FindAssets("t:PictureData");
        List<PictureData> dataList = new List<PictureData>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            PictureData pd = AssetDatabase.LoadAssetAtPath<PictureData>(path);
            if (pd != null) dataList.Add(pd);
        }
        carousel.pictureDataList = dataList.ToArray();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Done!",
            $"Picture Select scene created!\n\n" +
            $"Found {dataList.Count} PictureData objects.\n\n" +
            "Save as 'PictureSelect' (Ctrl+S)",
            "OK");
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
