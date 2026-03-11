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
    private List<Sprite>  sprites  = new List<Sprite>  { null, null, null };
    private List<string>  names    = new List<string>  { "Phong cảnh", "Mèo dễ thương", "Cá vàng" };
    private List<int>     regions  = new List<int>     { 6, 5, 5 };

    [MenuItem("Paint Game/Setup Picture Select")]
    public static void ShowWindow() => GetWindow<PictureSelectSetup>("Picture Select Setup");

    void OnGUI()
    {
        GUILayout.Label("Picture Select Scene Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Picture slots
        for (int i = 0; i < sprites.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            sprites[i] = (Sprite)EditorGUILayout.ObjectField($"Picture {i+1}", sprites[i], typeof(Sprite), false, GUILayout.Width(320));
            names[i]   = EditorGUILayout.TextField(names[i], GUILayout.Width(120));
            regions[i] = EditorGUILayout.IntField(regions[i], GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Assign sprites from Assets/Sprites/Outline/\n" +
            "Recommended: coloring_book_outline, coloring_book_cat, coloring_book_fish\n" +
            "Fields: [Sprite] [Name] [Regions]",
            MessageType.Info);
        EditorGUILayout.Space();

        bool anySprite = false;
        foreach (var s in sprites) if (s != null) anySprite = true;
        GUI.enabled = anySprite;
        if (GUILayout.Button("Create Picture Select Scene", GUILayout.Height(40))) CreateScene();
        GUI.enabled = true;
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
        backBtn.GetComponent<Button>().onClick.AddListener(
            () => UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu"));

        // Title in top bar
        MakeText("TopTitle", topBar.transform,
            new Vector2(0.3f, 0), new Vector2(0.7f, 1),
            "Chọn tranh", 24, Color.white);

        // Section title
        MakeText("SectionTitle", canvasGO.transform,
            new Vector2(0.2f, 0.82f), new Vector2(0.8f, 0.94f),
            "Chọn bức tranh bạn muốn tô", 20, new Color(0.7f, 0.7f, 0.85f));

        // ── Carousel viewport ────────────────────────────────────
        // Viewport mask (clips cards outside bounds)
        var viewport = MakePanel("CarouselViewport", canvasGO.transform,
            new Vector2(0, 0.12f), new Vector2(1, 0.82f),
            Vector2.zero, Vector2.zero);
        viewport.GetComponent<Image>().color = new Color(0,0,0,0);
        viewport.AddComponent<RectMask2D>();

        // Content container (moves left/right)
        var contentGO = new GameObject("CarouselContent");
        contentGO.transform.SetParent(viewport.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.5f, 0);
        contentRT.anchorMax = new Vector2(0.5f, 1);
        contentRT.sizeDelta = new Vector2(0, 0);
        contentRT.anchoredPosition = Vector2.zero;

        // Add IEventSystemHandler routing to viewport for drag
        var carouselDragProxy = viewport.AddComponent<CarouselDragProxy>();

        // ── Create picture cards ─────────────────────────────────
        float cardWidth   = 420f;
        float cardHeight  = 380f;
        float cardSpacing = 520f;
        var cardList = new List<PictureCard>();

        int validCount = 0;
        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] == null) continue;

            // Card panel
            var cardGO = MakePanel($"Card_{i}", contentGO.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(cardWidth, cardHeight),
                new Vector2(validCount * cardSpacing, 0));
            cardGO.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.28f);

            // Shadow/border
            var borderGO = MakePanel("Border", cardGO.transform,
                Vector2.zero, Vector2.one,
                new Vector2(-6f, -6f), Vector2.zero);
            borderGO.GetComponent<Image>().color = new Color(0.55f, 0.35f, 0.85f, 0.5f);
            borderGO.transform.SetAsFirstSibling();

            // Picture image
            var imgGO = MakePanel("PictureImage", cardGO.transform,
                new Vector2(0.05f, 0.20f), new Vector2(0.95f, 0.95f),
                Vector2.zero, Vector2.zero);
            var imgComp = imgGO.GetComponent<Image>();
            imgComp.sprite = sprites[i];
            imgComp.preserveAspect = true;
            imgComp.color = Color.white;

            // Name label at bottom of card
            var nameGO = MakeText($"NameLabel_{i}", cardGO.transform,
                new Vector2(0, 0.02f), new Vector2(1, 0.20f),
                names[i], 22, new Color(0.9f, 0.9f, 1f));

            // PictureCard component
            var card = cardGO.AddComponent<PictureCard>();
            card.outlineSprite = sprites[i];
            card.pictureName   = names[i];
            card.totalRegions  = regions[i];
            card.cardImage     = imgComp;
            card.nameLabel     = nameGO;

            cardList.Add(card);
            validCount++;
        }

        // ── Bottom bar: Selected name + Choose button ─────────────
        var bottomBar = MakePanel("BottomBar", canvasGO.transform,
            new Vector2(0, 0), new Vector2(1, 0.13f),
            Vector2.zero, Vector2.zero);
        bottomBar.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.18f, 0.97f);

        var selectedNameText = MakeText("SelectedNameText", bottomBar.transform,
            new Vector2(0.05f, 0.45f), new Vector2(0.60f, 0.95f),
            validCount > 0 ? names[0] : "", 22, new Color(1f, 0.9f, 0.5f));

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
        carousel.cards             = cardList;
        carousel.selectButton      = chooseBtn.GetComponent<Button>();
        carousel.selectedNameText  = selectedNameText;
        carousel.cardSpacing       = cardSpacing;
        carousel.paintSceneName    = "PaintScene";

        // Wire arrow buttons
        leftArrow.GetComponent<Button>().onClick.AddListener(carousel.GoLeft);
        rightArrow.GetComponent<Button>().onClick.AddListener(carousel.GoRight);

        // Wire drag proxy
        carouselDragProxy.carousel = carousel;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Done!",
            "Picture Select scene created!\n\n" +
            "Save as 'PictureSelect' (Ctrl+S)\n\n" +
            "Then add all 3 scenes to File → Build Settings:\n" +
            "  0: MainMenu\n  1: PictureSelect\n  2: PaintScene",
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
