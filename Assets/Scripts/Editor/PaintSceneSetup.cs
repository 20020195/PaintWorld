using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor utility to build the Paint Game scene.
/// Menu: Paint Game → Setup Scene
/// </summary>
public class PaintSceneSetup : EditorWindow
{
    private Sprite outlineSprite;
    private int   totalRegions  = 6;
    private float cameraSize    = 4f;
    private float maxOrthoSize  = 10f;

    [MenuItem("Paint Game/Setup Scene")]
    public static void ShowWindow() => GetWindow<PaintSceneSetup>("Paint Scene Setup");

    void OnGUI()
    {
        GUILayout.Label("Paint Game - Scene Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        outlineSprite = (Sprite)EditorGUILayout.ObjectField("Outline Sprite", outlineSprite, typeof(Sprite), false);
        totalRegions  = EditorGUILayout.IntField("Total Regions", totalRegions);
        cameraSize    = EditorGUILayout.FloatField("Camera Size", cameraSize);
        EditorGUILayout.Space();

        if (outlineSprite == null)
            EditorGUILayout.HelpBox("Assign the Outline Sprite from Assets/Sprites/Outline/", MessageType.Warning);

        GUI.enabled = outlineSprite != null;
        if (GUILayout.Button("Create Scene", GUILayout.Height(40))) CreateScene();
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "After setup:\n1. Save scene (Ctrl+S)\n2. Press Play\n3. Pick a color from the palette → click a white region",
            MessageType.Info);
    }

    void CreateScene()
    {
        // ── EventSystem ──────────────────────────────────────────
        if (FindObjectOfType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        // ── Camera ───────────────────────────────────────────────
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            mainCam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
        }
        mainCam.orthographic = true;
        mainCam.orthographicSize = cameraSize;
        mainCam.transform.position = new Vector3(0, 0, -10);
        mainCam.backgroundColor = new Color(0.94f, 0.94f, 0.96f);
        mainCam.clearFlags = CameraClearFlags.SolidColor;

        // Attach zoom/pan controller to the camera
        var zoomPan = mainCam.gameObject.GetComponent<CameraZoomPan>()
                   ?? mainCam.gameObject.AddComponent<CameraZoomPan>();
        zoomPan.minOrthoSize  = 0.5f;
        zoomPan.maxOrthoSize  = maxOrthoSize;
        zoomPan.zoomSpeed     = 1.25f;
        zoomPan.maxPanDistance = 15f;

        // ── Picture (PaintController) ────────────────────────────
        var pictureGO = new GameObject("PaintPicture");
        var pictureSR = pictureGO.AddComponent<SpriteRenderer>();
        pictureSR.sprite = outlineSprite;
        pictureGO.AddComponent<BoxCollider2D>();
        var paintController = pictureGO.AddComponent<PaintController>();
        paintController.totalRegions   = totalRegions;
        paintController.outlineTolerance = 0.2f;
        paintController.fillTolerance    = 0.8f;

        float scale = (cameraSize * 1.5f) / outlineSprite.bounds.size.y;
        pictureGO.transform.localScale = new Vector3(scale, scale, 1);
        pictureGO.transform.position   = new Vector3(0, 0.3f, 0);

        // ── Canvas ───────────────────────────────────────────────
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode      = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Top Bar: Back | Title | "Hoàn thành" ────────────────
        var topBar = MakePanel("TopBar", canvasGO.transform,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -54), Vector2.zero);
        topBar.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f, 0.96f);

        // Back button (left)
        var backBtn = MakeTextButton("BackButton", topBar.transform,
            new Vector2(0, 0), new Vector2(0, 1),
            new Vector2(56f, 0), new Vector2(100f, 0),
            "← Trở về", new Color(0.28f, 0.28f, 0.38f), 15);

        // Undo button (center-left)
        var undoBtn = MakeTextButton("UndoButton", topBar.transform,
            new Vector2(0.5f, 0), new Vector2(0.5f, 1),
            new Vector2(-60f, 0), new Vector2(90f, 0),
            "↶ Hủy bước", new Color(0.40f, 0.35f, 0.25f), 15);

        // Reset view button (center-right)
        var resetViewBtn = MakeTextButton("ResetViewButton", topBar.transform,
            new Vector2(0.5f, 0), new Vector2(0.5f, 1),
            new Vector2(60f, 0), new Vector2(120f, 0),
            "⊡ Reset View", new Color(0.25f, 0.35f, 0.55f), 15);

        // Complete button (right)
        var completeBtn = MakeTextButton("CompleteButton", topBar.transform,
            new Vector2(1, 0), new Vector2(1, 1),
            new Vector2(-90f, 0), new Vector2(160f, 0),
            "✔ Hoàn thành", new Color(0.22f, 0.72f, 0.40f), 17);

        // ── Palette Panel (bottom) ───────────────────────────────
        var palettePanel = MakePanel("ColorPalettePanel", canvasGO.transform,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 100), Vector2.zero);
        palettePanel.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.16f, 0.97f);
        var hlg = palettePanel.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment       = TextAnchor.MiddleCenter;
        hlg.spacing              = 8;
        hlg.padding              = new RectOffset(14, 14, 12, 12);
        hlg.childControlWidth    = false;
        hlg.childControlHeight   = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        var paletteUI = palettePanel.AddComponent<ColorPaletteUI>();
        paletteUI.paintController = paintController;

        // ── RGB Color Picker Panel (center) ──────────────────────
        var pickerPanel = BuildRGBPickerPanel(canvasGO.transform, out var rgbPicker);
        paletteUI.rgbColorPicker = rgbPicker;

        // ── Left Toolbar: Tools ────────────────────────────
        var toolbarPanel = MakePanel("LeftToolbar", canvasGO.transform,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(80, 320), new Vector2(50, 0));
        toolbarPanel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f, 0.96f);

        // Fill tool button
        var fillToolBtn = MakeTextButton("FillToolButton", toolbarPanel.transform,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -50), new Vector2(60, 60),
            "🪣", new Color(0.2f, 0.2f, 0.3f), 24);

        // Brush tool button
        var brushToolBtn = MakeTextButton("BrushToolButton", toolbarPanel.transform,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -120), new Vector2(60, 60),
            "🖌️", new Color(0.2f, 0.2f, 0.3f), 24);

        // Brush Settings Panel (Contains Slider)
        var sliderContainer = new GameObject("BrushSettings");
        sliderContainer.transform.SetParent(toolbarPanel.transform, false);
        var scRT = sliderContainer.AddComponent<RectTransform>();
        scRT.anchorMin = new Vector2(0.5f, 1);
        scRT.anchorMax = new Vector2(0.5f, 1);
        scRT.sizeDelta = new Vector2(60, 120);
        scRT.anchoredPosition = new Vector2(0, -220);

        DefaultControls.Resources uiRes = new DefaultControls.Resources();
        uiRes.standard = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        uiRes.background = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        uiRes.knob = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");

        var sliderGO = DefaultControls.CreateSlider(uiRes);
        sliderGO.transform.SetParent(sliderContainer.transform, false);
        var sRT = sliderGO.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0.5f, 0.5f);
        sRT.anchorMax = new Vector2(0.5f, 0.5f);
        sRT.sizeDelta = new Vector2(100, 20); // 100 length
        sRT.anchoredPosition = Vector2.zero;
        sRT.localEulerAngles = new Vector3(0, 0, 90f); // Rotate vertical
        
        var brushSlider = sliderGO.GetComponent<Slider>();
        brushSlider.minValue = 5;
        brushSlider.maxValue = 50;
        brushSlider.value = 15;

        // ── Win Panel ────────────────────────────────────────────
        var winPanel = MakePanel("WinPanel", canvasGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(380, 260), Vector2.zero);
        winPanel.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.20f, 0.97f);
        winPanel.SetActive(false);

        // Win title
        AddText(winPanel.transform, "WinTitle",
            new Vector2(0, 0.52f), new Vector2(1, 0.95f),
            "🎨 Hoàn thành!", 36, new Color(1f, 0.9f, 0.3f));

        // Win subtitle
        AddText(winPanel.transform, "WinSubtitle",
            new Vector2(0, 0.33f), new Vector2(1, 0.54f),
            "Bức tranh của bạn thật đẹp!", 18, new Color(0.75f, 0.75f, 0.75f));

        // Play Again button (inside win panel)
        var playAgainBtn = MakeTextButton("PlayAgainButton", winPanel.transform,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, 38f), new Vector2(200, 48f),
            "🔄 Chơi lại", new Color(0.28f, 0.68f, 0.40f), 20);

        // ── GameUIManager ────────────────────────────────────────
        var uiMgrGO = new GameObject("GameUIManager");
        var uiMgr   = uiMgrGO.AddComponent<GameUIManager>();
        uiMgr.winPanel        = winPanel;
        uiMgr.playAgainButton = playAgainBtn.GetComponent<Button>();
        uiMgr.undoButton      = undoBtn.GetComponent<Button>();
        uiMgr.completeButton  = completeBtn.GetComponent<Button>();
        uiMgr.backButton      = backBtn.GetComponent<Button>();
        uiMgr.toolbarPanelRect = toolbarPanel.GetComponent<RectTransform>();
        uiMgr.fillToolBtn     = fillToolBtn.GetComponent<Button>();
        uiMgr.brushToolBtn    = brushToolBtn.GetComponent<Button>();
        uiMgr.brushSettingsPanel = sliderContainer;
        uiMgr.brushSizeSlider = brushSlider;
        uiMgr.paintController = paintController;
        paintController.uiManager = uiMgr;

        // Wire "Reset View" button directly to CameraZoomPan
        var rvBtn = resetViewBtn.GetComponent<Button>();
        rvBtn.onClick.AddListener(zoomPan.ResetView);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Done!", "Scene created!\n\n1. Ctrl+S to save\n2. Press Play", "OK");
    }

    // ── RGB Picker Builder ────────────────────────────────────────

    GameObject BuildRGBPickerPanel(Transform canvasParent, out RGBColorPicker picker)
    {
        // Outer panel — centered, appears over everything
        var panel = MakePanel("RGBPickerPanel", canvasParent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(320, 340), Vector2.zero);
        panel.GetComponent<Image>().color = new Color(0.13f, 0.13f, 0.20f, 0.98f);

        var canvas = panel.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder    = 50;   // always on top
        panel.AddComponent<GraphicRaycaster>();

        // Title
        AddText(panel.transform, "PickerTitle",
            new Vector2(0, 0.87f), new Vector2(1, 1f),
            "Chọn màu", 22, Color.white);

        // Preview swatch (top area)
        var swatchGO = new GameObject("PreviewSwatch");
        swatchGO.transform.SetParent(panel.transform, false);
        var swatchRT = swatchGO.AddComponent<RectTransform>();
        swatchRT.anchorMin = new Vector2(0.35f, 0.73f);
        swatchRT.anchorMax = new Vector2(0.65f, 0.87f);
        swatchRT.offsetMin = swatchRT.offsetMax = Vector2.zero;
        var swatchImg = swatchGO.AddComponent<Image>();
        swatchImg.color = Color.white;

        // Sliders R, G, B
        Slider sliderR = BuildSlider(panel.transform, "SliderR",
            new Vector2(0.06f, 0.60f), new Vector2(0.94f, 0.71f),
            new Color(0.9f, 0.25f, 0.25f));

        Slider sliderG = BuildSlider(panel.transform, "SliderG",
            new Vector2(0.06f, 0.46f), new Vector2(0.94f, 0.57f),
            new Color(0.25f, 0.8f, 0.35f));

        Slider sliderB = BuildSlider(panel.transform, "SliderB",
            new Vector2(0.06f, 0.32f), new Vector2(0.94f, 0.43f),
            new Color(0.25f, 0.55f, 0.95f));

        // Value labels (right-aligned beside each slider)
        var lblR = AddText(panel.transform, "LabelR",
            new Vector2(0, 0.62f), new Vector2(0.08f, 0.72f),
            "R 255", 14, new Color(0.9f, 0.4f, 0.4f));
        var lblG = AddText(panel.transform, "LabelG",
            new Vector2(0, 0.48f), new Vector2(0.08f, 0.58f),
            "G 255", 14, new Color(0.4f, 0.9f, 0.5f));
        var lblB = AddText(panel.transform, "LabelB",
            new Vector2(0, 0.34f), new Vector2(0.08f, 0.44f),
            "B 255", 14, new Color(0.5f, 0.7f, 1f));

        // Confirm button
        var confirmBtn = MakeTextButton("ConfirmButton", panel.transform,
            new Vector2(0.55f, 0), new Vector2(0.55f, 0),
            new Vector2(0, 22f), new Vector2(120, 40f),
            "✔ Chọn", new Color(0.22f, 0.70f, 0.40f), 17);

        // Cancel button
        var cancelBtn = MakeTextButton("CancelButton", panel.transform,
            new Vector2(0.45f, 0), new Vector2(0.45f, 0),
            new Vector2(0, 22f), new Vector2(100, 40f),
            "✗ Hủy", new Color(0.55f, 0.20f, 0.20f), 17);

        // Attach RGBColorPicker script
        picker = panel.AddComponent<RGBColorPicker>();
        picker.sliderR       = sliderR;
        picker.sliderG       = sliderG;
        picker.sliderB       = sliderB;
        picker.labelR        = lblR;
        picker.labelG        = lblG;
        picker.labelB        = lblB;
        picker.previewSwatch = swatchImg;
        picker.confirmButton = confirmBtn.GetComponent<Button>();
        picker.cancelButton  = cancelBtn.GetComponent<Button>();

        panel.SetActive(false); // hidden by default
        return panel;
    }

    // ── Slider Factory ────────────────────────────────────────────

    Slider BuildSlider(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Color fillColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(8, 0);
        rt.offsetMax = new Vector2(-8, 0);

        var slider = go.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value    = 1f;

        // Track background
        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.25f, 0.25f, 0.32f);

        // Fill area
        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        var faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
        faRT.offsetMin = faRT.offsetMax = Vector2.zero;

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fRT = fill.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero;
        fRT.anchorMax = new Vector2(0f, 1f);
        fRT.offsetMin = fRT.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = fillColor;
        slider.fillRect = fRT;

        // Handle
        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(go.transform, false);
        var haRT = handleArea.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.offsetMin = haRT.offsetMax = Vector2.zero;

        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var hRT = handle.AddComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(16, 0);
        var hImg = handle.AddComponent<Image>();
        hImg.color = Color.white;
        slider.handleRect = hRT;
        slider.targetGraphic = hImg;

        return slider;
    }

    // ── Utility Helpers ───────────────────────────────────────────

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

    /// <summary>Button anchored by a pivot point, with given size.</summary>
    GameObject MakeTextButton(string name, Transform parent,
        Vector2 pivotAnchorMin, Vector2 pivotAnchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta,
        string label, Color bgColor, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin       = pivotAnchorMin;
        rt.anchorMax       = pivotAnchorMax;
        rt.pivot           = new Vector2(0.5f, 0.5f);
        rt.sizeDelta       = sizeDelta;
        rt.anchoredPosition = anchoredPos;
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        // Text child
        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var tRT = txtGO.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;
        var txt = txtGO.AddComponent<Text>();
        txt.text      = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize  = fontSize;
        txt.color     = Color.white;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return go;
    }

    Text AddText(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        string content, int fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var txt = go.AddComponent<Text>();
        txt.text      = content;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize  = fontSize;
        txt.color     = color;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return txt;
    }
}
