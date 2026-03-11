using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles texture-based flood fill painting on the picture.
/// Attach this to the GameObject that has the picture SpriteRenderer.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class PaintController : MonoBehaviour
{
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
        // On subsequent calls (reset), restore from that saved backup so we
        // never accidentally copy the already-painted texture.
        if (originalPixels == null)
        {
            Texture2D original = spriteRenderer.sprite.texture;
            texWidth  = original.width;
            texHeight = original.height;
            originalPixels = original.GetPixels32();
        }

        // Create / recreate the writable RGBA32 paint texture
        paintTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        paintTexture.filterMode = FilterMode.Bilinear;

        // Copy pixels (creates a fresh clone of original each time)
        pixels = (Color32[])originalPixels.Clone();
        paintTexture.SetPixels32(pixels);
        paintTexture.Apply();

        // Replace the sprite with one using our mutable texture
        float ppu = spriteRenderer.sprite.pixelsPerUnit;
        Sprite newSprite = Sprite.Create(paintTexture,
            new Rect(0, 0, texWidth, texHeight),
            new Vector2(0.5f, 0.5f), ppu);
        spriteRenderer.sprite = newSprite;

        // Fit the BoxCollider2D to the sprite size
        GetComponent<BoxCollider2D>().size = spriteRenderer.sprite.bounds.size;
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

        TryPaint(px, py);
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

            // Skip if already the EXACT final color we want
            // But since we multiply by brightness, the final color varies.
            // We just check if it's already processed to avoid infinite loops.
            if (current.r == fillColor.r && current.g == fillColor.g && current.b == fillColor.b) continue;

            // Apply color but multiply by original brightness to preserve anti-aliasing!
            float brightness = orig.r / 255f;
            byte newR = (byte)(fillColor.r * brightness);
            byte newG = (byte)(fillColor.g * brightness);
            byte newB = (byte)(fillColor.b * brightness);
            
            Color32 newColor = new Color32(newR, newG, newB, 255);

            // If we've already set this exact pixel color, stop (loop prevention)
            if (current.r == newColor.r && current.g == newColor.g && current.b == newColor.b) continue;

            pixels[idx] = newColor;
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
        InitTexture();

        if (uiManager != null)
            uiManager.UpdateProgress(0, totalRegions);
    }
}
