using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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

    [Header("Settings")]
    [Tooltip("Total distinct paintable regions in the image")]
    public int totalRegions = 6;

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
    private int texWidth, texHeight;
    private int paintedRegions = 0;
    private Color selectedColor = Color.blue;
    [Header("Drawing History")]
    private List<Color32[]> undoStack = new List<Color32[]>();
    private const int MAX_UNDO_STEPS = 10;
    private bool isGameComplete = false;

    // Brush tracking
    private bool isDraggingBrush = false;
    private Color32[] currentStrokeUndoState;
    private Vector2Int lastDragPos = new Vector2Int(-1, -1);

    // For tracking which regions have been painted
    // We sample a point; if it was white and becomes colored, it's a new region
    private static readonly Color32 WHITE = new Color32(255, 255, 255, 255);

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Load data from PictureSelect if available
        if (GameData.selectedSprite != null)
        {
            spriteRenderer.sprite = GameData.selectedSprite;
            totalRegions = GameData.totalRegions;
        }

        InitTexture();
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
            float brightness = (c.r + c.g + c.b) / (255f * 3f);
            
            // Map brightness to alpha: 0 brightness (black) = 255 alpha. 1 brightness (white) = 0 alpha.
            byte a = (byte)Mathf.Clamp(255 - (brightness * 255f), 0, 255);
            outlinePixels[i] = new Color32(c.r, c.g, c.b, a);
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
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = WHITE; // Fill bottom layer with white
        }
        paintTexture.SetPixels32(pixels);
        paintTexture.Apply();

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

    void OnMouseDown()
    {
        if (isGameComplete) return;
        if (selectedColor == default) return;

        // Block painting when the click lands on any UI element (e.g. RGB picker, palette)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Convert mouse position to world point
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0;

        // Convert world position to local position of the sprite
        Vector3 localPos = transform.InverseTransformPoint(worldPos);

        // Convert local pos to UV: sprite pivot is (0.5, 0.5)
        Bounds b = spriteRenderer.sprite.bounds;
        float u = (localPos.x - b.min.x) / b.size.x;
        float v = (localPos.y - b.min.y) / b.size.y;

        if (u < 0 || u > 1 || v < 0 || v > 1) return;

        int px = Mathf.FloorToInt(u * texWidth);
        int py = Mathf.FloorToInt(v * texHeight);
        px = Mathf.Clamp(px, 0, texWidth - 1);
        py = Mathf.Clamp(py, 0, texHeight - 1);

        if (currentTool == PaintTool.Fill)
        {
            TryPaint(px, py);
        }
        else if (currentTool == PaintTool.Brush)
        {
            // Start brush stroke
            isDraggingBrush = true;
            currentStrokeUndoState = (Color32[])pixels.Clone();
            lastDragPos = new Vector2Int(px, py);
            ApplyBrush(px, py);
        }
    }

    void OnMouseDrag()
    {
        if (isGameComplete || selectedColor == default || currentTool != PaintTool.Brush) return;
        if (!isDraggingBrush) return;
        
        // Let user drag out of UI (no block while dragging)
        
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        Bounds b = spriteRenderer.sprite.bounds;
        
        float u = (localPos.x - b.min.x) / b.size.x;
        float v = (localPos.y - b.min.y) / b.size.y;

        int px = Mathf.FloorToInt(u * texWidth);
        int py = Mathf.FloorToInt(v * texHeight);
        px = Mathf.Clamp(px, 0, texWidth - 1);
        py = Mathf.Clamp(py, 0, texHeight - 1);

        // Interpolate between last point and this point to prevent gaps if mouse moves fast
        float distance = Vector2.Distance(lastDragPos, new Vector2(px, py));
        int steps = Mathf.Max(1, Mathf.FloorToInt(distance / (brushRadius * 0.5f)));

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            int lerpX = Mathf.RoundToInt(Mathf.Lerp(lastDragPos.x, px, t));
            int lerpY = Mathf.RoundToInt(Mathf.Lerp(lastDragPos.y, py, t));
            ApplyBrush(lerpX, lerpY);
        }

        lastDragPos = new Vector2Int(px, py);
    }

    void OnMouseUp()
    {
        if (isDraggingBrush && currentTool == PaintTool.Brush)
        {
            // End brush stroke, commit to undo
            isDraggingBrush = false;
            undoStack.Add(currentStrokeUndoState);
            if (undoStack.Count > MAX_UNDO_STEPS)
                undoStack.RemoveAt(0);
        }
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
        Color32 targetPixel = pixels[startY * texWidth + startX];

        // Don't paint on outline pixels (dark pixels)
        if (IsOutlinePixel(targetPixel)) return;

        // Check if this region was previously white (unpainted)
        bool wasWhite = IsWhitePixel(targetPixel);

        Color32 fillColor = new Color32(
            (byte)(selectedColor.r * 255),
            (byte)(selectedColor.g * 255),
            (byte)(selectedColor.b * 255),
            255);

        // Don't repaint same color
        if (ColorDistance(targetPixel, fillColor) < 0.01f) return;

        // Clone current state for undo
        Color32[] prevState = (Color32[])pixels.Clone();

        // Run flood fill
        bool painted = FloodFill(startX, startY, targetPixel, fillColor);

        if (painted)
        {
            // Push to undo stack
            undoStack.Add(prevState);
            if (undoStack.Count > MAX_UNDO_STEPS)
                undoStack.RemoveAt(0);

            if (wasWhite)
            {
                paintedRegions++;
                if (uiManager != null)
                    uiManager.UpdateProgress(paintedRegions, totalRegions);
            }
        }
    }

    void ApplyBrush(int centerX, int centerY)
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
                    int targetX = centerX + x;
                    int targetY = centerY + y;

                    if (targetX >= 0 && targetX < texWidth && targetY >= 0 && targetY < texHeight)
                    {
                        int idx = targetY * texWidth + targetX;
                        Color32 orig = originalPixels[idx];

                        // Skip painting on actual outline border pixels 
                        if (IsOutlinePixel(orig)) continue;

                        pixels[idx] = brushColor;
                        painted = true;
                    }
                }
            }
        }

        if (painted)
        {
            paintTexture.SetPixels32(pixels);
            paintTexture.Apply();
        }
    }

    /// <summary>Reverts the picture to the previous state.</summary>
    public void Undo()
    {
        if (isGameComplete || undoStack.Count == 0) return;

        // Pop last state
        pixels = undoStack[undoStack.Count - 1];
        undoStack.RemoveAt(undoStack.Count - 1);

        // Apply to texture
        paintTexture.SetPixels32(pixels);
        paintTexture.Apply();
    }

    bool FloodFill(int startX, int startY, Color32 targetColor, Color32 fillColor)
    {
        if (ColorDistance(targetColor, fillColor) < 0.01f) return false;

        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(startX, startY));

        bool anyFilled = false;

        while (stack.Count > 0)
        {
            Vector2Int pos = stack.Pop();
            int x = pos.x;
            int y = pos.y;

            if (x < 0 || x >= texWidth || y < 0 || y >= texHeight) continue;

            int idx = y * texWidth + x;
            Color32 current = pixels[idx];
            Color32 orig = originalPixels[idx];

            // Use original pixel to firmly detect walls
            if (IsOutlinePixel(orig)) continue;

            // Check if this pixel is part of the region we are replacing
            if (ColorDistance(current, targetColor) > fillTolerance) continue;

            // Prevent infinite loop if we are refilling with a very similar color
            if (current.r == fillColor.r && current.g == fillColor.g && current.b == fillColor.b) continue;

            // Apply solid color (the bottom layer is masked by the top outline layer, making it look flush and anti-aliased)
            pixels[idx] = fillColor;
            anyFilled = true;

            stack.Push(new Vector2Int(x + 1, y));
            stack.Push(new Vector2Int(x - 1, y));
            stack.Push(new Vector2Int(x, y + 1));
            stack.Push(new Vector2Int(x, y - 1));
        }

        if (anyFilled)
        {
            paintTexture.SetPixels32(pixels);
            paintTexture.Apply();
        }

        return anyFilled;
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

    float ColorDistance(Color32 a, Color32 b)
    {
        float dr = (a.r - b.r) / 255f;
        float dg = (a.g - b.g) / 255f;
        float db = (a.b - b.b) / 255f;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }

    public void ResetPainting()
    {
        paintedRegions = 0;
        isGameComplete = false;
        undoStack.Clear();
        InitTexture();

        if (uiManager != null)
            uiManager.UpdateProgress(0, totalRegions);
    }
}
