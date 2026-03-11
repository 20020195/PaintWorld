using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Zoom (scroll wheel) and Pan (middle-mouse drag OR right-mouse drag) for
/// the orthographic Main Camera. Attach to the Main Camera GameObject.
/// 
/// Controls:
///   Scroll wheel        → zoom in / out  
///   Middle-mouse drag   → pan
///   Right-mouse drag    → pan (alternative)
///   Double-click scroll (scroll fast) or Home key → reset view
/// </summary>
public class CameraZoomPan : MonoBehaviour
{
    [Header("Zoom")]
    [Tooltip("Minimum orthographic size (most zoomed in)")]
    public float minOrthoSize = 0.5f;

    [Tooltip("Maximum orthographic size (most zoomed out)")]
    public float maxOrthoSize = 10f;

    [Tooltip("Scroll wheel zoom speed")]
    public float zoomSpeed = 1.2f;

    [Tooltip("Smooth zoom interpolation speed")]
    public float zoomSmoothSpeed = 12f;

    [Header("Pan")]
    [Tooltip("Pan drag speed multiplier")]
    public float panSpeed = 20f;

    [Header("Limits")]
    [Tooltip("How far the camera can be panned from the origin")]
    public float maxPanDistance = 12f;

    [Header("Reset")]
    [Tooltip("Press this key to reset zoom + pan to default")]
    public KeyCode resetKey = KeyCode.Home;
    public Button resetViewButton;

    // ── private state ────────────────────────────────────────────
    private Camera cam;
    private float targetOrthoSize;
    private float defaultOrthoSize;
    private Vector3 defaultPosition;

    // For pixel-perfect panning
    private Vector3 panOriginWorld;
    private Vector3 panOriginCamPos;
    private bool isPanning;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null || !cam.orthographic)
        {
            Debug.LogWarning("[CameraZoomPan] Requires an orthographic Camera on the same GameObject.");
            enabled = false;
            return;
        }

        defaultOrthoSize = cam.orthographicSize;
        targetOrthoSize  = cam.orthographicSize;
        defaultPosition  = transform.position;

        if (resetViewButton != null)
        {
            resetViewButton.onClick.AddListener(ResetView);
        }
    }

    void Update()
    {
        HandleZoom();
        HandlePan();
        HandleReset();

        // Smoothly approach target zoom
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrthoSize,
            Time.unscaledDeltaTime * zoomSmoothSpeed);
    }

    // ── Zoom ─────────────────────────────────────────────────────

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.001f) return;

        // Don't zoom when pointer is over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // Zoom toward / away from the mouse cursor position
        Vector3 mouseWorldBefore = cam.ScreenToWorldPoint(Input.mousePosition);

        if (scroll > 0)
            targetOrthoSize /= zoomSpeed;
        else
            targetOrthoSize *= zoomSpeed;

        targetOrthoSize = Mathf.Clamp(targetOrthoSize, minOrthoSize, maxOrthoSize);

        // Reposition camera so the point under the cursor stays fixed
        // (do it next frame via coroutine-free approach — apply instantly for now)
        cam.orthographicSize = targetOrthoSize;
        Vector3 mouseWorldAfter = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector3 delta = mouseWorldBefore - mouseWorldAfter;
        Vector3 newPos = transform.position + delta;
        newPos = ClampPosition(newPos);
        transform.position = newPos;
        defaultPosition = defaultPosition; // don't update default
    }

    // ── Pan ──────────────────────────────────────────────────────

    void HandlePan()
    {
        bool panButtonDown = Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1);
        bool panButton     = Input.GetMouseButton(2)     || Input.GetMouseButton(1);

        if (panButtonDown)
        {
            // Don't start pan on UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            isPanning = true;
        }

        if (isPanning && panButton)
        {
            // Use raw screen-pixel delta instead of ScreenToWorldPoint.
            // Avoids feedback loop: moving the camera would shift the world-space
            // result of ScreenToWorldPoint, causing the jitter.
            Vector2 screenDelta = new Vector2(
                Input.GetAxis("Mouse X"),
                Input.GetAxis("Mouse Y"));

            // World units per screen pixel for an orthographic camera
            float worldPerPixel = (cam.orthographicSize * 2f) / Screen.height;

            Vector3 move = new Vector3(
                -screenDelta.x * worldPerPixel * panSpeed,
                -screenDelta.y * worldPerPixel * panSpeed,
                0f);

            Vector3 newPos = ClampPosition(transform.position + move);
            transform.position = newPos;
        }

        if (Input.GetMouseButtonUp(2) || Input.GetMouseButtonUp(1))
            isPanning = false;
    }

    // ── Reset ────────────────────────────────────────────────────

    void HandleReset()
    {
        if (Input.GetKeyDown(resetKey))
            ResetView();
    }

    public void ResetView()
    {
        targetOrthoSize = defaultOrthoSize;
        transform.position = defaultPosition;
    }

    // ── Helpers ──────────────────────────────────────────────────

    Vector3 ClampPosition(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, -maxPanDistance, maxPanDistance);
        pos.y = Mathf.Clamp(pos.y, -maxPanDistance, maxPanDistance);
        pos.z = defaultPosition.z; // keep Z fixed
        return pos;
    }
}
