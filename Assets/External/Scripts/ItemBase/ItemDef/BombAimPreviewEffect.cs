using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class BombAimPreviewEffect : MonoBehaviour
{
    private const string TrajectoryChildName = "TrajectoryLine";
    private const string RangeChildName = "ExplosionRange";

    [Header("Runtime References")]
    [SerializeField] private LineRenderer trajectoryRenderer;
    [SerializeField] private LineRenderer rangeRenderer;

    [Header("Bomb Parameters")]
    [SerializeField] private BombTrajectoryType trajectoryType = BombTrajectoryType.Parabolic;
    [SerializeField, Min(0.1f)] private float launchSpeed = 12f;
    [SerializeField, Min(0f)] private float additionalUpwardSpeed = 4f;
    [SerializeField, Min(0.1f)] private float maxLifetime = 5f;
    [SerializeField] private LayerMask impactMask = ~0;
    [SerializeField, Min(0.1f)] private float explosionRadius = 3f;

    [Header("Prediction")]
    [SerializeField, Min(2)] private int trajectorySegmentCount = 32;
    [SerializeField, Min(0.01f)] private float trajectoryStepTime = 0.05f;
    [SerializeField, Min(0f)] private float spawnForwardOffset = 0.6f;
    [SerializeField, Min(0f)] private float spawnUpwardOffset = 0.2f;
    [SerializeField, Min(0f)] private float rangeSurfaceOffset = 0.05f;
    [SerializeField, Range(8, 128)] private int rangeCircleSegments = 48;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private bool hideOnStart = true;

    [Header("Style")]
    [SerializeField, Min(0.001f)] private float trajectoryWidth = 0.06f;
    [SerializeField, Min(0.001f)] private float rangeWidth = 0.045f;
    [SerializeField] private Color trajectoryColor = new Color(0.3f, 0.95f, 1f, 0.95f);
    [SerializeField] private Color rangeColor = new Color(1f, 0.45f, 0.15f, 0.95f);

    private Material previewMaterial;

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

    private void OnDestroy()
    {
        if (previewMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(previewMaterial);
        else
            DestroyImmediate(previewMaterial);
    }

    public void ConfigureFromDefinition(BombItemDefinition definition)
    {
        if (definition == null)
            return;

        trajectoryType = definition.TrajectoryType;
        launchSpeed = definition.LaunchSpeed;
        additionalUpwardSpeed = definition.AdditionalUpwardSpeed;
        maxLifetime = definition.MaxLifetime;
        impactMask = definition.ImpactMask;
        explosionRadius = definition.ExplosionRadius;
    }

    public void Configure(
        BombTrajectoryType newTrajectoryType,
        float newLaunchSpeed,
        float newAdditionalUpwardSpeed,
        float newMaxLifetime,
        LayerMask newImpactMask,
        float newExplosionRadius)
    {
        trajectoryType = newTrajectoryType;
        launchSpeed = Mathf.Max(0.1f, newLaunchSpeed);
        additionalUpwardSpeed = Mathf.Max(0f, newAdditionalUpwardSpeed);
        maxLifetime = Mathf.Max(0.1f, newMaxLifetime);
        impactMask = newImpactMask;
        explosionRadius = Mathf.Max(0.1f, newExplosionRadius);
    }

    public void RenderPreview(Transform spawnTransform, Vector3 aimDirection)
    {
        if (spawnTransform == null)
        {
            HidePreview();
            return;
        }

        RenderPreview(spawnTransform.position, spawnTransform.forward, aimDirection);
    }

    public void RenderPreview(Vector3 spawnOrigin, Vector3 forwardFallback, Vector3 aimDirection)
    {
        EnsureVisualObjects();
        ApplyVisualStyle();

        Vector3 launchDirection = ResolveLaunchDirection(aimDirection, forwardFallback);
        Vector3 previewStart = spawnOrigin + ResolveSpawnOffsetDirection(launchDirection, forwardFallback) * spawnForwardOffset + Vector3.up * spawnUpwardOffset;

        Vector3[] points = new Vector3[Mathf.Max(2, trajectorySegmentCount + 1)];
        int pointCount = 1;
        points[0] = previewStart;

        Vector3 currentPosition = previewStart;
        Vector3 currentVelocity = launchDirection * launchSpeed;
        if (trajectoryType == BombTrajectoryType.Parabolic)
            currentVelocity += Vector3.up * additionalUpwardSpeed;

        Vector3 impactPoint = currentPosition;
        Vector3 impactNormal = Vector3.up;
        bool hitImpact = false;
        float elapsed = 0f;
        Vector3 gravity = trajectoryType == BombTrajectoryType.Parabolic ? Physics.gravity : Vector3.zero;

        while (elapsed < maxLifetime && pointCount < points.Length)
        {
            float step = Mathf.Min(trajectoryStepTime, maxLifetime - elapsed);
            Vector3 nextVelocity = currentVelocity + gravity * step;
            Vector3 nextPosition = currentPosition + currentVelocity * step + gravity * (0.5f * step * step);

            if (Physics.Linecast(
                    currentPosition,
                    nextPosition,
                    out RaycastHit hit,
                    impactMask,
                    triggerInteraction))
            {
                impactPoint = hit.point;
                impactNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
                points[pointCount++] = impactPoint;
                hitImpact = true;
                break;
            }

            points[pointCount++] = nextPosition;
            currentPosition = nextPosition;
            currentVelocity = nextVelocity;
            impactPoint = currentPosition;
            elapsed += step;
        }

        UpdateTrajectoryLine(points, pointCount);
        UpdateRangeCircle(impactPoint, hitImpact ? impactNormal : Vector3.up);
        SetVisible(true);
    }

    public void HidePreview()
    {
        SetVisible(false);
    }

    [ContextMenu("Preview From Self Forward")]
    private void PreviewFromSelfForward()
    {
        RenderPreview(transform.position, transform.forward, transform.forward);
    }

    private void EnsureVisualObjects()
    {
        trajectoryRenderer = EnsureLineRenderer(
            trajectoryRenderer,
            TrajectoryChildName,
            loop: false);

        rangeRenderer = EnsureLineRenderer(
            rangeRenderer,
            RangeChildName,
            loop: true);
    }

    private void ResolveExistingVisualObjects()
    {
        trajectoryRenderer = ResolveExistingLineRenderer(trajectoryRenderer, TrajectoryChildName);
        rangeRenderer = ResolveExistingLineRenderer(rangeRenderer, RangeChildName);
    }

    private LineRenderer ResolveExistingLineRenderer(LineRenderer renderer, string childName)
    {
        if (renderer != null)
            return renderer;

        Transform child = transform.Find(childName);
        if (child == null)
            return null;

        return child.GetComponent<LineRenderer>();
    }

    private LineRenderer EnsureLineRenderer(LineRenderer renderer, string childName, bool loop)
    {
        if (renderer == null)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                GameObject childObject = new GameObject(childName);
                child = childObject.transform;
                child.SetParent(transform, false);
            }

            renderer = child.GetComponent<LineRenderer>();
            if (renderer == null)
                renderer = child.gameObject.AddComponent<LineRenderer>();
        }

        renderer.useWorldSpace = true;
        renderer.loop = loop;
        renderer.alignment = LineAlignment.View;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.numCornerVertices = 4;
        renderer.numCapVertices = 4;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;
        renderer.sharedMaterial = GetOrCreatePreviewMaterial();
        return renderer;
    }

    private void ApplyVisualStyle()
    {
        if (trajectoryRenderer == null || rangeRenderer == null)
            return;

        ApplyRendererStyle(trajectoryRenderer, trajectoryWidth, trajectoryColor);
        ApplyRendererStyle(rangeRenderer, rangeWidth, rangeColor);
    }

    private void ApplyRendererStyle(LineRenderer renderer, float width, Color color)
    {
        renderer.widthMultiplier = Mathf.Max(0.001f, width);
        renderer.startColor = color;
        renderer.endColor = color;
        renderer.sharedMaterial = GetOrCreatePreviewMaterial();
    }

    private Material GetOrCreatePreviewMaterial()
    {
        if (previewMaterial != null)
            return previewMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        previewMaterial = new Material(shader)
        {
            name = "BombAimPreviewMaterial"
        };

        previewMaterial.hideFlags = HideFlags.HideAndDontSave;
        return previewMaterial;
    }

    private void UpdateTrajectoryLine(Vector3[] points, int pointCount)
    {
        if (trajectoryRenderer == null)
            return;

        trajectoryRenderer.positionCount = pointCount;
        for (int i = 0; i < pointCount; i++)
        {
            trajectoryRenderer.SetPosition(i, points[i]);
        }
    }

    private void UpdateRangeCircle(Vector3 center, Vector3 normal)
    {
        if (rangeRenderer == null)
            return;

        Vector3 planeNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        Vector3 tangent = Vector3.Cross(planeNormal, Vector3.up);
        if (tangent.sqrMagnitude <= 0.0001f)
            tangent = Vector3.Cross(planeNormal, Vector3.right);

        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(planeNormal, tangent).normalized;
        Vector3 elevatedCenter = center + planeNormal * rangeSurfaceOffset;

        rangeRenderer.positionCount = rangeCircleSegments;
        for (int i = 0; i < rangeCircleSegments; i++)
        {
            float t = (float)i / rangeCircleSegments;
            float angle = t * Mathf.PI * 2f;
            Vector3 offset = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
            rangeRenderer.SetPosition(i, elevatedCenter + offset * explosionRadius);
        }
    }

    private void SetVisible(bool visible)
    {
        if (trajectoryRenderer != null)
            trajectoryRenderer.enabled = visible;

        if (rangeRenderer != null)
            rangeRenderer.enabled = visible;
    }

    private Vector3 ResolveLaunchDirection(Vector3 aimDirection, Vector3 forwardFallback)
    {
        Vector3 rawLaunchDirection = aimDirection.sqrMagnitude > 0.0001f
            ? aimDirection.normalized
            : forwardFallback;

        if (rawLaunchDirection.sqrMagnitude <= 0.0001f)
            return Vector3.forward;

        return rawLaunchDirection.normalized;
    }

    private Vector3 ResolveSpawnOffsetDirection(Vector3 launchDirection, Vector3 forwardFallback)
    {
        Vector3 flatDirection = new Vector3(launchDirection.x, 0f, launchDirection.z);
        if (flatDirection.sqrMagnitude <= 0.0001f)
        {
            flatDirection = new Vector3(forwardFallback.x, 0f, forwardFallback.z);
        }

        if (flatDirection.sqrMagnitude <= 0.0001f)
            return Vector3.forward;

        return flatDirection.normalized;
    }
}
