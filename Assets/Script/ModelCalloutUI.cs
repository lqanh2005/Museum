using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModelCalloutUI : MonoBehaviour
{
    public enum LabelSide
    {
        Left,
        Right
    }

    [Serializable]
    public class CalloutTarget
    {
        public string label;
        public Transform worldTarget;
        public LabelSide side = LabelSide.Left;
        public float verticalOffset;
        public Vector3 targetWorldOffset = Vector3.zero;
        public bool offsetInLocalSpace = true;
    }

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("Bật nếu Canvas.worldCamera khác camera đang vẽ model (vd nhiều camera). Khi đó chỉ dùng Target Camera để WorldToScreen.")]
    [SerializeField] private bool useTargetCameraOnlyForProjection;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform root;
    [SerializeField] private TMP_Text labelPrefab;
    [SerializeField] private Image linePrefab;
    [Header("Dual Camera Setup (Optional)")]
    [Tooltip("Camera gameplay/FPS chính (không dùng để chiếu callout).")]
    [SerializeField] private Camera fpsCamera;
    [Tooltip("Camera nhìn từ trên, dùng để chiếu và render callout.")]
    [SerializeField] private Camera calloutTopCamera;

    [Header("Layout")]
    [SerializeField] private List<CalloutTarget> callouts = new List<CalloutTarget>();
    [SerializeField] private float horizontalDistance = 280f;
    [SerializeField] private float edgePadding = 24f;
    [SerializeField] private float lineThickness = 2f;
    [SerializeField] private float minDepthVisible = 0.01f;
    [SerializeField] private bool hideWhenBehindCamera = true;
    [SerializeField] private bool useAccurateWorldTarget = true;

    [Header("Activation")]
    [SerializeField] private bool requireActivationInput = true;
    [SerializeField] private bool useToggleInput = true;
    [SerializeField] private KeyCode activationKey = KeyCode.Tab;

    private readonly List<CalloutRuntime> runtimeItems = new List<CalloutRuntime>();
    private bool isActivated = true;

    private class CalloutRuntime
    {
        public CalloutTarget data;
        public RectTransform labelRect;
        public TMP_Text labelText;
        public RectTransform lineRect;
    }

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (root == null && canvas != null) root = canvas.transform as RectTransform;
    }

    private void Start()
    {
        BuildRuntime();
        isActivated = !requireActivationInput;
    }

    private void OnValidate()
    {
        lineThickness = Mathf.Max(1f, lineThickness);
        edgePadding = Mathf.Max(0f, edgePadding);
        horizontalDistance = Mathf.Max(50f, horizontalDistance);
        minDepthVisible = Mathf.Max(0.001f, minDepthVisible);
    }

    private void LateUpdate()
    {
        if (canvas == null || root == null) return;
        if (GetCanvasCamera() == null) return;

        UpdateActivationState();
        if (!isActivated)
        {
            SetAllVisible(false);
            return;
        }

        UpdateCalloutPositions();
    }

    [ContextMenu("Rebuild Callouts")]
    public void RebuildCallouts()
    {
        ClearRuntime();
        BuildRuntime();
    }

    /// <summary>
    /// Gọi khi bạn có nhiều camera và đổi camera đang hiển thị model (cả 3 đều có thể render cùng model).
    /// Gán camera đang active / đang vẽ layer model — chú thích mới khớp hình.
    /// Với Canvas Screen Space - Camera, tùy chọn đồng bộ luôn Render Camera của Canvas.
    /// </summary>
    public void SetRenderingCamera(Camera cam, bool syncCanvasWorldCamera = true)
    {
        targetCamera = cam;
        if (syncCanvasWorldCamera && canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera && cam != null)
            canvas.worldCamera = cam;
    }

    /// <summary>
    /// Dùng khi bạn có camera FPS và camera Top riêng cho callout.
    /// Gọi 1 lần sau khi gán camera trong Inspector.
    /// </summary>
    public void ApplyDualCameraSetup(bool syncCanvasWorldCamera = true)
    {
        if (calloutTopCamera == null)
        {
            Debug.LogWarning("ModelCalloutUI: Chưa gán calloutTopCamera.");
            return;
        }

        // Trong mode 2 camera, luôn lấy camera Top để WorldToScreen.
        useTargetCameraOnlyForProjection = true;
        targetCamera = calloutTopCamera;

        if (syncCanvasWorldCamera && canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            canvas.worldCamera = calloutTopCamera;
    }

    private void BuildRuntime()
    {
        ClearRuntime();

        if (labelPrefab == null || linePrefab == null || root == null) return;

        foreach (CalloutTarget item in callouts)
        {
            if (item == null || item.worldTarget == null) continue;

            TMP_Text textInstance = Instantiate(labelPrefab, root);
            textInstance.gameObject.SetActive(true);
            textInstance.text = item.label;

            Image lineInstance = Instantiate(linePrefab, root);
            lineInstance.gameObject.SetActive(true);
            lineInstance.raycastTarget = false;

            runtimeItems.Add(new CalloutRuntime
            {
                data = item,
                labelRect = textInstance.rectTransform,
                labelText = textInstance,
                lineRect = lineInstance.rectTransform
            });
        }
    }

    private void ClearRuntime()
    {
        for (int i = 0; i < runtimeItems.Count; i++)
        {
            if (runtimeItems[i].labelRect != null) Destroy(runtimeItems[i].labelRect.gameObject);
            if (runtimeItems[i].lineRect != null) Destroy(runtimeItems[i].lineRect.gameObject);
        }
        runtimeItems.Clear();
    }

    private void UpdateCalloutPositions()
    {
        Rect rootRect = root.rect;
        Camera cam = GetCanvasCamera();
        if (cam == null) return;

        foreach (CalloutRuntime item in runtimeItems)
        {
            if (item.data == null || item.data.worldTarget == null) continue;

            Vector3 worldPos = GetAnchorWorldPosition(item.data);
            Vector3 screenPoint = cam.WorldToScreenPoint(worldPos);
            bool visible = screenPoint.z > minDepthVisible;

            if (!visible && hideWhenBehindCamera)
            {
                SetVisible(item, false);
                continue;
            }

            SetVisible(item, true);

            if (!TryConvertWorldToCanvasPoint(worldPos, out Vector2 anchorPos))
            {
                continue;
            }

            float targetLabelY = anchorPos.y + item.data.verticalOffset;
            float labelX;

            Vector2 preferredSize = item.labelText.GetPreferredValues(item.data.label);
            float labelWidth = Mathf.Max(item.labelRect.rect.width, preferredSize.x);

            float centerX = anchorPos.x;
            float desiredX = item.data.side == LabelSide.Left
                ? centerX - horizontalDistance
                : centerX + horizontalDistance;

            float minX = rootRect.xMin + edgePadding + labelWidth * 0.5f;
            float maxX = rootRect.xMax - edgePadding - labelWidth * 0.5f;
            labelX = Mathf.Clamp(desiredX, minX, maxX);

            item.labelRect.anchoredPosition = new Vector2(labelX, targetLabelY);
            UpdateLine(item, anchorPos);
        }
    }

    private bool TryConvertWorldToCanvasPoint(Vector3 worldPos, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;

        Camera cam = GetCanvasCamera();
        if (cam == null) return false;

        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        if (screenPos.z <= minDepthVisible && hideWhenBehindCamera) return false;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPos, null, out localPoint))
                return true;

            return TryConvertWithScreenFallback(screenPos, out localPoint);
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPos, cam, out localPoint))
            return true;

        return TryConvertWithScreenFallback(screenPos, out localPoint);
    }

    private Camera GetCanvasCamera()
    {
        if (useTargetCameraOnlyForProjection && targetCamera != null)
            return targetCamera;

        Camera cam = canvas != null && canvas.worldCamera != null ? canvas.worldCamera : targetCamera;
        if (cam == null) cam = Camera.main;
        return cam;
    }

    private Vector3 GetAnchorWorldPosition(CalloutTarget data)
    {
        if (data == null || data.worldTarget == null) return Vector3.zero;
        Transform target = data.worldTarget;

        if (!useAccurateWorldTarget || target == null) return target != null ? target.position : Vector3.zero;

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null && renderer.bounds.size != Vector3.zero)
        {
            Bounds b = renderer.bounds;
            Vector3 anchor = new Vector3(b.center.x, b.max.y, b.center.z);
            return ApplyTargetOffset(anchor, target, data);
        }

        Renderer[] childRenderers = target.GetComponentsInChildren<Renderer>();
        if (childRenderers != null && childRenderers.Length > 0)
        {
            bool hasBounds = false;
            Bounds combined = new Bounds(target.position, Vector3.zero);
            for (int i = 0; i < childRenderers.Length; i++)
            {
                if (childRenderers[i] == null || childRenderers[i].bounds.size == Vector3.zero) continue;
                if (!hasBounds)
                {
                    combined = childRenderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(childRenderers[i].bounds);
                }
            }

            if (hasBounds)
            {
                Vector3 anchor = new Vector3(combined.center.x, combined.max.y, combined.center.z);
                return ApplyTargetOffset(anchor, target, data);
            }
        }

        return ApplyTargetOffset(target.position, target, data);
    }

    private Vector3 ApplyTargetOffset(Vector3 anchor, Transform target, CalloutTarget data)
    {
        if (data == null) return anchor;

        if (data.offsetInLocalSpace && target != null)
        {
            return anchor + target.TransformDirection(data.targetWorldOffset);
        }

        return anchor + data.targetWorldOffset;
    }

    private bool TryConvertWithScreenFallback(Vector3 screenPos, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;

        float rootWidth = root.rect.width;
        float rootHeight = root.rect.height;
        if (rootWidth <= 0f || rootHeight <= 0f) return false;

        if (Screen.width <= 0 || Screen.height <= 0) return false;

        localPoint = new Vector2(
            (screenPos.x / Screen.width - 0.5f) * rootWidth,
            (screenPos.y / Screen.height - 0.5f) * rootHeight
        );
        return true;
    }

    private void UpdateActivationState()
    {
        if (!requireActivationInput)
        {
            isActivated = true;
            return;
        }

        if (useToggleInput)
        {
            if (Input.GetKeyDown(activationKey))
            {
                isActivated = !isActivated;
            }
        }
        else
        {
            isActivated = Input.GetKey(activationKey);
        }
    }

    private void UpdateLine(CalloutRuntime item, Vector2 sourcePoint)
    {
        Vector2 labelPos = item.labelRect.anchoredPosition;
        float labelHalfWidth = item.labelRect.rect.width * 0.5f;
        float labelEdgeX = item.data.side == LabelSide.Left
            ? labelPos.x + labelHalfWidth
            : labelPos.x - labelHalfWidth;

        Vector2 endPoint = new Vector2(labelEdgeX, labelPos.y);
        Vector2 delta = endPoint - sourcePoint;
        float distance = delta.magnitude;

        if (distance < 0.001f) return;

        item.lineRect.anchoredPosition = (sourcePoint + endPoint) * 0.5f;
        item.lineRect.sizeDelta = new Vector2(distance, lineThickness);

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        item.lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private static void SetVisible(CalloutRuntime item, bool value)
    {
        if (item.labelRect != null && item.labelRect.gameObject.activeSelf != value)
            item.labelRect.gameObject.SetActive(value);

        if (item.lineRect != null && item.lineRect.gameObject.activeSelf != value)
            item.lineRect.gameObject.SetActive(value);
    }

    private void SetAllVisible(bool value)
    {
        foreach (CalloutRuntime item in runtimeItems)
        {
            SetVisible(item, value);
        }
    }
}
