using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CrosshairAimPreview : MonoBehaviour
{
    private const string CrosshairRootName = "CrosshairRoot";

    [Header("Runtime References")]
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private RectTransform crosshairRoot;
    [SerializeField] private Image crosshairImage;

    [Header("Style")]
    [SerializeField] private Sprite crosshairSprite;
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private Vector2 crosshairSize = new Vector2(32f, 32f);
    [SerializeField] private int sortingOrder = 1000;
    [SerializeField] private bool hideOnStart = true;

    private Sprite fallbackSprite;

    private void Reset()
    {
        EnsureVisualObjects();
        ApplyVisualStyle();
        HidePreview();
    }

    private void Awake()
    {
        EnsureVisualObjects();
        ApplyVisualStyle();

        if (hideOnStart)
            HidePreview();
    }

    private void OnValidate()
    {
        ResolveExistingVisualObjects();
        ApplyVisualStyle();
    }

    public void ShowPreview()
    {
        if (overlayCanvas != null)
            overlayCanvas.enabled = true;

        if (crosshairImage != null)
            crosshairImage.enabled = true;
    }

    public void HidePreview()
    {
        if (crosshairImage != null)
            crosshairImage.enabled = false;

        if (overlayCanvas != null)
            overlayCanvas.enabled = false;
    }

    private void EnsureVisualObjects()
    {
        if (overlayCanvas == null)
        {
            overlayCanvas = GetComponent<Canvas>();
            if (overlayCanvas == null)
                overlayCanvas = gameObject.AddComponent<Canvas>();
        }

        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = sortingOrder;

        if (GetComponent<CanvasScaler>() == null)
            gameObject.AddComponent<CanvasScaler>();

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (crosshairRoot == null)
        {
            Transform child = transform.Find(CrosshairRootName);
            if (child == null)
            {
                GameObject childObject = new GameObject(CrosshairRootName);
                child = childObject.transform;
                child.SetParent(transform, false);
            }

            crosshairRoot = child as RectTransform;
            if (crosshairRoot == null)
                crosshairRoot = child.gameObject.AddComponent<RectTransform>();
        }

        crosshairRoot.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairRoot.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairRoot.pivot = new Vector2(0.5f, 0.5f);
        crosshairRoot.anchoredPosition = Vector2.zero;
        crosshairRoot.sizeDelta = crosshairSize;

        if (crosshairImage == null)
        {
            crosshairImage = crosshairRoot.GetComponent<Image>();
            if (crosshairImage == null)
                crosshairImage = crosshairRoot.gameObject.AddComponent<Image>();
        }

        crosshairImage.raycastTarget = false;
    }

    private void ResolveExistingVisualObjects()
    {
        if (overlayCanvas == null)
            overlayCanvas = GetComponent<Canvas>();

        if (crosshairRoot == null)
        {
            Transform child = transform.Find(CrosshairRootName);
            if (child != null)
                crosshairRoot = child as RectTransform;
        }

        if (crosshairImage == null && crosshairRoot != null)
            crosshairImage = crosshairRoot.GetComponent<Image>();
    }

    private void ApplyVisualStyle()
    {
        if (overlayCanvas != null)
            overlayCanvas.sortingOrder = sortingOrder;

        if (crosshairRoot != null)
            crosshairRoot.sizeDelta = crosshairSize;

        if (crosshairImage == null)
            return;

        crosshairImage.sprite = crosshairSprite != null ? crosshairSprite : GetFallbackSprite();
        crosshairImage.color = crosshairColor;
        crosshairImage.type = Image.Type.Simple;
        crosshairImage.preserveAspect = true;
    }

    private Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
            return fallbackSprite;

        fallbackSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        return fallbackSprite;
    }
}
