using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum PaintTool
{
    Fill,
    Brush
}

/// <summary>
/// Handles texture-based flood fill painting on the picture.
/// Attach this to the GameObject that has the picture SpriteRenderer.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class PaintController : MonoBehaviour
{
    [Header("Tool Settings")]
    public PaintTool currentTool = PaintTool.Fill;
    [Tooltip("Radius of the brush tool in pixels")]
    public int brushRadius = 15;

    [Tooltip("Tolerance for detecting outline pixels (0-1). Black pixels within this threshold are considered outline.")]
    public float outlineTolerance = 0.2f;

    [Tooltip("Tolerance for flood fill (pixels within this color distance are filled together)")]
    public float fillTolerance = 0.8f;

    [Header("References")]
    public GameUIManager uiManager;

    // Internal state
    private SpriteRenderer spriteRenderer;
    private Texture2D paintTexture;
    private Color32[] pixels;          // current (mutable) pixel array
    private Color32[] originalPixels;  // backup of original, never modified
    private int[] visitedArr;          // flood fill tracking
    private int visitCounter = 0;
    private int texWidth, texHeight;
    private Color selectedColor = Color.blue;
    [Header("Drawing History")]
    [Tooltip("Maximum number of undo steps. Higher = more RAM usage (especially for large images).")]
    public int maxUndoSteps = 20;

    // Diff-based undo: stores only the pixels that changed, not the full texture.
    // Each UndoPatch = array of (pixelIndex, oldColor) — tiny for small strokes/fills.
    private struct PixelDiff { public int idx; public Color32 oldColor; }
    private List<PixelDiff[]> undoStack = new List<PixelDiff[]>();

    public bool IsGameComplete { get; private set; } = false;

    // Brush tracking: accumulate diffs across a whole stroke
    private bool isDraggingBrush = false;
    private Dictionary<int, Color32> strokeOriginals = new Dictionary<int, Color32>(); // idx → original color before this stroke
    private Vector2Int lastDragPos = new Vector2Int(-1, -1);

    /// <summary>True while the user is actively drawing a brush stroke. 
    /// CameraZoomPan reads this to block 1-finger pan during painting.</summary>
    public static bool IsBrushActive { get; private set; }

    // For tracking which regions have been painted
    // We sample a point; if it was white and becomes colored, it's a new region
    private static readonly Color32 WHITE = new Color32(255, 255, 255, 255);

    // Tracks which touch fingerIds started on a UI element — these must not paint the canvas.
    private readonly HashSet<int> uiBlockedFingers = new HashSet<int>();

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (GameData.selectedSprite != null)
        {
            spriteRenderer.sprite = GameData.selectedSprite;
        }

        InitTexture();

#if !UNITY_EDITOR && !UNITY_STANDALONE
        // Disable this so OnMouseDown is NOT triggered by touches.
        // We handle all touch input manually in Update() to control UI blocking order.
        Input.simulateMouseWithTouches = false;
#endif
    }

    void Update()
    {
#if !UNITY_EDITOR && !UNITY_STANDALONE
        HandleTouchPainting();
#endif
    }

    void OnApplicationQuit()
    {
        SaveCurrentProgress();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveCurrentProgress();
    }

    public void SaveCurrentProgress()
    {
        if (IsGameComplete || string.IsNullOrEmpty(GameData.selectedPictureName) || paintTexture == null) return;
        
        Color32[] currentColors = paintTexture.GetPixels32();
        Color32[] composited = new Color32[currentColors.Length];

        for(int i = 0; i < currentColors.Length; i++)
        {
            Color32 orig = originalPixels[i];
            byte a = GetOutlineAlpha(orig);
            
            Color32 cColor = currentColors[i];
            float alpha = a / 255f;
            byte r = (byte)(0 * alpha + cColor.r * (1 - alpha));
            byte g = (byte)(0 * alpha + cColor.g * (1 - alpha));
            byte b = (byte)(0 * alpha + cColor.b * (1 - alpha));
            
            composited[i] = new Color32(r, g, b, 255);
        }

        Texture2D previewTex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        previewTex.SetPixels32(composited);
        previewTex.Apply();

        SaveSystem.SavePaintProgress(GameData.selectedPictureName, paintTexture, previewTex);
        Destroy(previewTex);
    }

    private bool wasMultiTouchThisGesture = false;
    private bool isTouchBlocked = false;

    void HandleTouchPainting()
    {
        if (IsGameComplete || selectedColor == default) return;
        
        if (Input.touchCount == 0)
        {
            wasMultiTouchThisGesture = false;
            isTouchBlocked = false;
            return;
        }

        // --- ABORT PAINTING IF MULTIPLE FINGERS (E.G. ZOOMING) ---
        if (Input.touchCount > 1)
        {
            wasMultiTouchThisGesture = true;
            if (isDraggingBrush)
            {
                isDraggingBrush = false;
                IsBrushActive = false;
                
                // Revert any pixels modified by this accidental stroke
                if (strokeOriginals.Count > 0)
                {
                    foreach (var kv in strokeOriginals)
                        pixels[kv.Key] = kv.Value;
                        
                    strokeOriginals.Clear();
                    paintTexture.SetPixels32(pixels);
                    paintTexture.Apply();
                }
            }
            return;
        }

        // Ignore remaining single finger movements if it was part of a pinch zoom gesture
        if (wasMultiTouchThisGesture) return;

        Touch t = Input.GetTouch(0); // only track primary finger for painting

        if (t.phase == TouchPhase.Began)
        {
            if (IsScreenPosOverUI(t.position))
            {
                isTouchBlocked = true;
                return;
            }

            isTouchBlocked = false;
            if (!TryGetTexelFromScreen(t.position, out int px, out int py)) return;

            if (currentTool == PaintTool.Brush)
            {
                isDraggingBrush = true;
                IsBrushActive = true;
                strokeOriginals.Clear();
                lastDragPos = new Vector2Int(px, py);
                if (ApplyBrush(px, py))
                {
                    paintTexture.SetPixels32(pixels);
                    paintTexture.Apply();
                }
            }
        }
        else if ((t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                 && isDraggingBrush && currentTool == PaintTool.Brush && !isTouchBlocked)
        {
            if (!TryGetTexelFromScreen(t.position, out int px, out int py)) return;

            float dist = Vector2.Distance(lastDragPos, new Vector2(px, py));
            int steps = Mathf.Max(1, Mathf.FloorToInt(dist / Mathf.Max(1f, brushRadius * 0.5f)));
            bool dirty = false;
            for (int i = 1; i <= steps; i++)
            {
                float frac = (float)i / steps;
                if (ApplyBrush(
                    Mathf.RoundToInt(Mathf.Lerp(lastDragPos.x, px, frac)),
                    Mathf.RoundToInt(Mathf.Lerp(lastDragPos.y, py, frac))))
                {
                    dirty = true;
                }
            }
            
            if (dirty)
            {
                paintTexture.SetPixels32(pixels);
                paintTexture.Apply();
            }
            
            lastDragPos = new Vector2Int(px, py);
        }
        else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
        {
            if (currentTool == PaintTool.Brush && isDraggingBrush)
            {
                isDraggingBrush = false;
                IsBrushActive  = false;
                if (strokeOriginals.Count > 0)
                {
                    var pd = new PixelDiff[strokeOriginals.Count];
                    int i = 0;
                    foreach (var kv in strokeOriginals)
                        pd[i++] = new PixelDiff { idx = kv.Key, oldColor = kv.Value };
                    PushUndo(pd);
                    strokeOriginals.Clear();
                }
            }
            else if (currentTool == PaintTool.Fill && !isTouchBlocked && t.phase == TouchPhase.Ended)
            {
                if (TryGetTexelFromScreen(t.position, out int px, out int py))
                {
                    TryPaint(px, py);
                }
            }
        }
    }

    void InitTexture()
    {
        if (spriteRenderer.sprite == null) return;

        // On first init, read from the original sprite asset and save a backup.
        if (originalPixels == null)
        {
            Texture2D original = spriteRenderer.sprite.texture;
            texWidth  = original.width;
            texHeight = original.height;
            originalPixels = original.GetPixels32();
            visitedArr = new int[originalPixels.Length];
        }

        float ppu = spriteRenderer.sprite.pixelsPerUnit;
        Vector2 pivot = new Vector2(0.5f, 0.5f);

        // ── 1. Top Layer (Outline/Stencil) ──
        // Convert original image to a transparent stencil (white becomes transparent, dark stays dark)
        Texture2D outlineTex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        outlineTex.filterMode = FilterMode.Bilinear;
        Color32[] outlinePixels = new Color32[originalPixels.Length];

        for (int i = 0; i < originalPixels.Length; i++)
        {
            Color32 c = originalPixels[i];
            byte a = GetOutlineAlpha(c);
            
            // Use pure black (0,0,0) for the outline, using only alpha to blend. This completely eliminates white/gray fringing.
            outlinePixels[i] = new Color32(0, 0, 0, a);
        }
        
        outlineTex.SetPixels32(outlinePixels);
        outlineTex.Apply();

        Sprite topSprite = Sprite.Create(outlineTex, new Rect(0, 0, texWidth, texHeight), pivot, ppu);
        spriteRenderer.sprite = topSprite;
        spriteRenderer.sortingOrder = 1; // Put outline on top
        GetComponent<BoxCollider2D>().size = topSprite.bounds.size;

        // ── 2. Bottom Layer (Colors) ──
        // Create a solid white writable texture
        paintTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        paintTexture.filterMode = FilterMode.Point; // Crisp edges underneath the stencil

        pixels = new Color32[originalPixels.Length];

        bool loaded = false;
        if (!string.IsNullOrEmpty(GameData.selectedPictureName))
        {
            loaded = SaveSystem.LoadPaintProgress(GameData.selectedPictureName, paintTexture);
        }

        if (loaded)
        {
            pixels = paintTexture.GetPixels32();
        }
        else
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = WHITE; // Fill bottom layer with white
            }
            paintTexture.SetPixels32(pixels);
            paintTexture.Apply();
        }

        // Assign bottom layer to a child GameObject
        Transform existingBottom = transform.Find("ColorLayer");
        GameObject bottomGO;
        if (existingBottom != null)
        {
            bottomGO = existingBottom.gameObject;
        }
        else
        {
            bottomGO = new GameObject("ColorLayer");
            bottomGO.transform.SetParent(this.transform);
            bottomGO.transform.localPosition = Vector3.zero;
            bottomGO.transform.localScale = Vector3.one;
            bottomGO.AddComponent<SpriteRenderer>();
        }

        SpriteRenderer bottomSR = bottomGO.GetComponent<SpriteRenderer>();
        Sprite bottomSprite = Sprite.Create(paintTexture, new Rect(0, 0, texWidth, texHeight), pivot, ppu);
        bottomSR.sprite = bottomSprite;
        bottomSR.sortingOrder = 0; // Behind the outline
    }

    // PC ONLY — On mobile these are not called because simulateMouseWithTouches = false.
    void OnMouseDown()
    {
        if (IsGameComplete) return;
        if (selectedColor == default) return;
        if (IsPointerOverUI()) return;

        if (!TryGetInputTexelCoord(out int px, out int py)) return;

        if (currentTool == PaintTool.Fill)
        {
            TryPaint(px, py);
        }
        else if (currentTool == PaintTool.Brush)
        {
            isDraggingBrush = true;
            IsBrushActive = true;
            strokeOriginals.Clear();
            lastDragPos = new Vector2Int(px, py);
            if (ApplyBrush(px, py))
            {
                paintTexture.SetPixels32(pixels);
                paintTexture.Apply();
            }
        }
    }

    void OnMouseDrag()
    {
        if (IsGameComplete || selectedColor == default || currentTool != PaintTool.Brush) return;
        if (!isDraggingBrush) return;

        if (!TryGetInputTexelCoord(out int px, out int py)) return;

        // Interpolate between last point and this point to prevent gaps if mouse moves fast
        float distance = Vector2.Distance(lastDragPos, new Vector2(px, py));
        int steps = Mathf.Max(1, Mathf.FloorToInt(distance / Mathf.Max(1f, brushRadius * 0.5f)));
        bool dirty = false;

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            int lerpX = Mathf.RoundToInt(Mathf.Lerp(lastDragPos.x, px, t));
            int lerpY = Mathf.RoundToInt(Mathf.Lerp(lastDragPos.y, py, t));
            if (ApplyBrush(lerpX, lerpY)) dirty = true;
        }

        if (dirty)
        {
            paintTexture.SetPixels32(pixels);
            paintTexture.Apply();
        }

        lastDragPos = new Vector2Int(px, py);
    }

    void OnMouseUp()
    {
        if (isDraggingBrush && currentTool == PaintTool.Brush)
        {
            isDraggingBrush = false;
            IsBrushActive = false;
            if (strokeOriginals.Count > 0)
            {
                var pd = new PixelDiff[strokeOriginals.Count];
                int i = 0;
                foreach (var kv in strokeOriginals)
                    pd[i++] = new PixelDiff { idx = kv.Key, oldColor = kv.Value };
                PushUndo(pd);
                strokeOriginals.Clear();
            }
        }
    }

    // ── Input helpers (PC mouse + Mobile touch) ─────────

    /// Returns true when any current pointer/touch is over a UI element.
    bool IsPointerOverUI()
    {
        // Mouse (PC)
        if (Input.touchCount == 0)
            return IsScreenPosOverUI(Input.mousePosition);

        // Touch: check all active fingers
        foreach (Touch t in Input.touches)
            if (IsScreenPosOverUI(t.position)) return true;

        return false;
    }

    /// Reliable UI hit test using EventSystem.RaycastAll.
    bool IsScreenPosOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        return results.Count > 0;
    }

    /// Converts an explicit screen position to texel coordinates on this sprite.
    bool TryGetTexelFromScreen(Vector2 screenPos, out int px, out int py)
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0));
        worldPos.z = 0;
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        Bounds b = spriteRenderer.sprite.bounds;
        float u = (localPos.x - b.min.x) / b.size.x;
        float v = (localPos.y - b.min.y) / b.size.y;
        if (u < 0 || u > 1 || v < 0 || v > 1) { px = py = 0; return false; }
        px = Mathf.Clamp(Mathf.FloorToInt(u * texWidth),  0, texWidth  - 1);
        py = Mathf.Clamp(Mathf.FloorToInt(v * texHeight), 0, texHeight - 1);
        return true;
    }

    /// Gets the primary input position (touch or mouse) and converts it to texel
    /// coordinates on this sprite. Returns false if out of bounds.
    bool TryGetInputTexelCoord(out int px, out int py)
    {
        Vector2 screenPos;
#if UNITY_EDITOR || UNITY_STANDALONE
        screenPos = Input.mousePosition;
#else
        if (Input.touchCount == 0) { px = py = 0; return false; }
        screenPos = Input.GetTouch(0).position;
#endif
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0));
        worldPos.z = 0;
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        Bounds b = spriteRenderer.sprite.bounds;
        float u = (localPos.x - b.min.x) / b.size.x;
        float v = (localPos.y - b.min.y) / b.size.y;
        if (u < 0 || u > 1 || v < 0 || v > 1) { px = py = 0; return false; }
        px = Mathf.Clamp(Mathf.FloorToInt(u * texWidth),  0, texWidth  - 1);
        py = Mathf.Clamp(Mathf.FloorToInt(v * texHeight), 0, texHeight - 1);
        return true;
    }

    /// Returns true if every active touch started on a UI element (i.e., should be blocked from painting).
    bool IsTouchBlockedByUI()
    {
        if (Input.touchCount == 0) return false;
        // Block if ALL active touches are UI-touch (avoids blocking when one finger is on the canvas)
        foreach (Touch t in Input.touches)
            if (!uiBlockedFingers.Contains(t.fingerId)) return false;
        return true;
    }

    public void SetTool(PaintTool tool)
    {
        currentTool = tool;
    }

    public void SetBrushRadius(float radius)
    {
        brushRadius = Mathf.RoundToInt(radius);
    }

    public void SetSelectedColor(Color color)
    {
        selectedColor = color;
    }

    void TryPaint(int startX, int startY)
    {
        int targetIdx = startY * texWidth + startX;
        Color32 targetPixel = pixels[targetIdx];
        Color32 origPixel = originalPixels[targetIdx];

        // Don't paint on outline pixels (dark pixels in the original sprite)
        if (IsOutlinePixel(origPixel)) return;

        // Check if this region was previously white (unpainted)
        bool wasWhite = IsWhitePixel(targetPixel);

        Color32 fillColor = new Color32(
            (byte)(selectedColor.r * 255),
            (byte)(selectedColor.g * 255),
            (byte)(selectedColor.b * 255),
            255);

        // Don't repaint same color
        if (ColorDistance(targetPixel, fillColor) < 0.01f) return;

        // Run flood fill — returns the diff
        PixelDiff[] diff = FloodFill(startX, startY, targetPixel, fillColor);

        if (diff != null && diff.Length > 0)
        {
            PushUndo(diff);
        }
    }

    bool ApplyBrush(int centerX, int centerY)
    {
        bool painted = false;
        Color32 brushColor = new Color32(
            (byte)(selectedColor.r * 255),
            (byte)(selectedColor.g * 255),
            (byte)(selectedColor.b * 255),
            255);

        for (int y = -brushRadius; y <= brushRadius; y++)
        {
            for (int x = -brushRadius; x <= brushRadius; x++)
            {
                if (x * x + y * y <= brushRadius * brushRadius)
                {
                    int tx = centerX + x;
                    int ty = centerY + y;

                    if (tx >= 0 && tx < texWidth && ty >= 0 && ty < texHeight)
                    {
                        int idx = ty * texWidth + tx;

                        // Record original color the first time we touch this pixel in this stroke
                        if (!strokeOriginals.ContainsKey(idx))
                            strokeOriginals[idx] = pixels[idx];

                        pixels[idx] = brushColor;
                        painted = true;
                    }
                }
            }
        }

        return painted;
    }

    /// <summary>Reverts the picture to the previous state.</summary>
    public void Undo()
    {
        if (IsGameComplete || undoStack.Count == 0) return;

        PixelDiff[] diff = undoStack[undoStack.Count - 1];
        undoStack.RemoveAt(undoStack.Count - 1);

        // Restore only the changed pixels
        foreach (var d in diff)
            pixels[d.idx] = d.oldColor;

        paintTexture.SetPixels32(pixels);
        paintTexture.Apply();
    }

    void PushUndo(PixelDiff[] diff)
    {
        undoStack.Add(diff);
        if (undoStack.Count > maxUndoSteps)
            undoStack.RemoveAt(0);
    }

    PixelDiff[] FloodFill(int startX, int startY, Color32 targetColor, Color32 fillColor)
    {
        if (ColorDistance(targetColor, fillColor) < 0.01f) return null;

        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(startX, startY));

        visitCounter++;
        var diff = new List<PixelDiff>();

        while (stack.Count > 0)
        {
            Vector2Int pos = stack.Pop();
            int x = pos.x;
            int y = pos.y;

            if (x < 0 || x >= texWidth || y < 0 || y >= texHeight) continue;

            int idx = y * texWidth + x;
            
            if (visitedArr[idx] == visitCounter) continue;
            visitedArr[idx] = visitCounter;

            Color32 current = pixels[idx];
            Color32 orig = originalPixels[idx];

            if (IsOutlinePixel(orig))
            {
                if (current.r != fillColor.r || current.g != fillColor.g || current.b != fillColor.b)
                {
                    diff.Add(new PixelDiff { idx = idx, oldColor = current });
                    pixels[idx] = fillColor;
                }
                continue;
            }
            if (ColorDistance(current, targetColor) > fillTolerance) continue;

            if (current.r != fillColor.r || current.g != fillColor.g || current.b != fillColor.b)
            {
                diff.Add(new PixelDiff { idx = idx, oldColor = current });
                pixels[idx] = fillColor;
            }

            stack.Push(new Vector2Int(x + 1, y));
            stack.Push(new Vector2Int(x - 1, y));
            stack.Push(new Vector2Int(x, y + 1));
            stack.Push(new Vector2Int(x, y - 1));
        }

        if (diff.Count > 0)
        {
            // --- DILATE 3 PIXELS TO HIDE GAPS UNDER OUTLINES ---
            int expandPixels = 3;
            List<int> currentFrontier = new List<int>(diff.Count);
            List<int> nextFrontier = new List<int>();

            for (int i = 0; i < diff.Count; i++) currentFrontier.Add(diff[i].idx);

            for (int step = 0; step < expandPixels; step++)
            {
                nextFrontier.Clear();
                for (int i = 0; i < currentFrontier.Count; i++)
                {
                    int idx = currentFrontier[i];
                    int tx = idx % texWidth;
                    int ty = idx / texWidth;

                    if (tx < texWidth - 1) TryDilate(idx + 1, fillColor, nextFrontier, diff);
                    if (tx > 0) TryDilate(idx - 1, fillColor, nextFrontier, diff);
                    if (ty < texHeight - 1) TryDilate(idx + texWidth, fillColor, nextFrontier, diff);
                    if (ty > 0) TryDilate(idx - texWidth, fillColor, nextFrontier, diff);
                }
                
                // Swap lists
                var temp = currentFrontier;
                currentFrontier = nextFrontier;
                nextFrontier = temp;
            }

            paintTexture.SetPixels32(pixels);
            paintTexture.Apply();
            return diff.ToArray();
        }

        return null;
    }

    void TryDilate(int n, Color32 fillColor, List<int> nextFrontier, List<PixelDiff> diff)
    {
        if (visitedArr[n] != visitCounter)
        {
            visitedArr[n] = visitCounter;
            Color32 orig = originalPixels[n];
            float brightness = (orig.r + orig.g + orig.b) / (255f * 3f);
            
            // Allow bleed ONLY into dark outline/gradient. Block bleed into white paper.
            // Brightness < 0.95f captures anti-aliasing pixels and thick black lines
            if (brightness < 0.95f) 
            {
                if (pixels[n].r != fillColor.r || pixels[n].g != fillColor.g || pixels[n].b != fillColor.b)
                {
                    diff.Add(new PixelDiff { idx = n, oldColor = pixels[n] });
                    pixels[n] = fillColor;
                }
                nextFrontier.Add(n);
            }
        }
    }

    bool IsOutlinePixel(Color32 c)
    {
        float brightness = (c.r + c.g + c.b) / (255f * 3f);
        return brightness < outlineTolerance;
    }

    bool IsWhitePixel(Color32 c)
    {
        return c.r > 200 && c.g > 200 && c.b > 200;
    }

    /// <summary>
    /// Computes outline alpha using perceptual luminance and contrast crushing 
    /// to eliminate JPEG/DXT compression noise in black/white areas.
    /// </summary>
    byte GetOutlineAlpha(Color32 c)
    {
        float lum = (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) / 255f;
        
        // Crush darks (<= 0.2) to pure opaque (255), and whites (>= 0.8) to pure transparent (0)
        float remapped = Mathf.InverseLerp(0.2f, 0.8f, lum);
        return (byte)Mathf.Clamp(255 - (remapped * 255f), 0, 255);
    }

    float ColorDistance(Color32 a, Color32 b)
    {
        float dr = (a.r - b.r) / 255f;
        float dg = (a.g - b.g) / 255f;
        float db = (a.b - b.b) / 255f;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }

    public void ResetPainting()
    {
        IsGameComplete = false;
        undoStack.Clear();

        if (!string.IsNullOrEmpty(GameData.selectedPictureName))
            SaveSystem.DeleteSave(GameData.selectedPictureName);

        InitTexture();
    }

    public void MarkComplete()
    {
        IsGameComplete = true;
        
        if (!string.IsNullOrEmpty(GameData.selectedPictureName))
            SaveSystem.DeleteSave(GameData.selectedPictureName);
    }
}
