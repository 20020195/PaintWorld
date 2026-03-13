using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Zoom and Pan for the orthographic Main Camera.
/// PC:     Scroll wheel to zoom, Middle/Right mouse drag to pan.
/// Mobile: Pinch (2 fingers) to zoom, 1-finger drag to pan.
/// Attach to the Main Camera GameObject.
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
    private bool isPanning;
    private Vector2 lastPanScreenPos;

    // Touch state
    private float lastPinchDistance = 0f;

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
            resetViewButton.onClick.AddListener(ResetView);
    }

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseZoom();
        HandleMousePan();
#else
        HandleTouchInput();
#endif
        HandleReset();

        // Smoothly approach target zoom
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrthoSize,
            Time.unscaledDeltaTime * zoomSmoothSpeed);
    }

    // ──────────────────────────── PC Mouse ────────────────────────────────

    void HandleMouseZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.001f) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Vector3 mouseWorldBefore = cam.ScreenToWorldPoint(Input.mousePosition);

        if (scroll > 0) targetOrthoSize /= zoomSpeed;
        else            targetOrthoSize *= zoomSpeed;

        targetOrthoSize = Mathf.Clamp(targetOrthoSize, minOrthoSize, maxOrthoSize);

        // Keep the point under the cursor fixed
        cam.orthographicSize = targetOrthoSize;
        Vector3 mouseWorldAfter = cam.ScreenToWorldPoint(Input.mousePosition);
        transform.position = ClampPosition(transform.position + (mouseWorldBefore - mouseWorldAfter));
    }

    void HandleMousePan()
    {
        bool panDown   = Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1);
        bool panHeld   = Input.GetMouseButton(2)     || Input.GetMouseButton(1);
        bool panUp     = Input.GetMouseButtonUp(2)   || Input.GetMouseButtonUp(1);

        if (panDown)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            isPanning       = true;
            lastPanScreenPos = Input.mousePosition;
        }

        if (isPanning && panHeld)
        {
            Vector2 screenDelta = new Vector2(
                Input.GetAxis("Mouse X"),
                Input.GetAxis("Mouse Y"));

            float worldPerPixel = (cam.orthographicSize * 2f) / Screen.height;
            Vector3 move = new Vector3(
                -screenDelta.x * worldPerPixel * panSpeed,
                -screenDelta.y * worldPerPixel * panSpeed, 0f);

            transform.position = ClampPosition(transform.position + move);
        }

        if (panUp) isPanning = false;
    }

    // ──────────────────────────── Mobile Touch ────────────────────────────

    void HandleTouchInput()
    {
        int touchCount = Input.touchCount;

        if (touchCount == 2)
        {
            isPanning = false; // Cancel any 1-finger pan when a second finger appears
            HandlePinchZoom();
        }
        else if (touchCount == 1)
        {
            lastPinchDistance = 0f; // Reset pinch when down to 1 finger
            HandleOneFingePan();
        }
        else
        {
            isPanning = false;
            lastPinchDistance = 0f;
        }
    }

    void HandlePinchZoom()
    {
        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);

        float currentDistance = Vector2.Distance(t0.position, t1.position);

        if (lastPinchDistance <= 0f)
        {
            lastPinchDistance = currentDistance;
            return;
        }

        float delta = currentDistance - lastPinchDistance;
        lastPinchDistance = currentDistance;

        // Midpoint in screen space for zoom-to-point
        Vector2 midScreen  = (t0.position + t1.position) * 0.5f;
        Vector3 midWorldBefore = cam.ScreenToWorldPoint(new Vector3(midScreen.x, midScreen.y, 0));

        // Pinch factor: larger distance = zoom in
        float pinchFactor = 1f - delta * 0.005f;
        targetOrthoSize = Mathf.Clamp(targetOrthoSize * pinchFactor, minOrthoSize, maxOrthoSize);

        // Instantly apply to get accurate world-space offset
        cam.orthographicSize = targetOrthoSize;
        Vector3 midWorldAfter = cam.ScreenToWorldPoint(new Vector3(midScreen.x, midScreen.y, 0));
        transform.position = ClampPosition(transform.position + (midWorldBefore - midWorldAfter));
    }

    void HandleOneFingePan()
    {
        Touch t = Input.GetTouch(0);

        // Do not pan if the user is actively drawing with brush
        if (PaintController.IsBrushActive) return;

        // Skip if over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId)) return;

        if (t.phase == TouchPhase.Began)
        {
            isPanning        = true;
            lastPanScreenPos = t.position;
        }
        else if (isPanning && (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary))
        {
            Vector2 delta = t.position - lastPanScreenPos;
            float worldPerPixel = (cam.orthographicSize * 2f) / Screen.height;
            Vector3 move = new Vector3(-delta.x * worldPerPixel, -delta.y * worldPerPixel, 0f);
            transform.position = ClampPosition(transform.position + move);
            lastPanScreenPos = t.position;
        }
        else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
        {
            isPanning = false;
        }
    }

    // ── Reset ────────────────────────────────────────────────────

    void HandleReset()
    {
        if (Input.GetKeyDown(resetKey))
            ResetView();
    }

    public void ResetView()
    {
        targetOrthoSize    = defaultOrthoSize;
        transform.position = defaultPosition;
    }

    // ── Helpers ──────────────────────────────────────────────────

    Vector3 ClampPosition(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, -maxPanDistance, maxPanDistance);
        pos.y = Mathf.Clamp(pos.y, -maxPanDistance, maxPanDistance);
        pos.z = defaultPosition.z;
        return pos;
    }
}
