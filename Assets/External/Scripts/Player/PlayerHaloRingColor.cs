using Photon.Pun;
using UnityEngine;

/// <summary>
/// Assigns a deterministic ring color per player (Photon owner actor number).
/// Uses a fixed palette that avoids strong red / green for team or UI convention.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PhotonView))]
public class PlayerHaloRingColor : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    /// <summary>Hand-tuned colors: no pure red/green; repeats safely for many players.</summary>
    private static readonly Color[] Palette =
    {
        new Color(0.25f, 0.55f, 1f),
        new Color(1f, 0.82f, 0.2f),
        new Color(0.55f, 0.3f, 1f),
        new Color(0.1f, 0.85f, 1f),
        new Color(1f, 0.48f, 0.12f),
        new Color(0.4f, 0.75f, 1f),
        new Color(1f, 0.42f, 0.78f),
        new Color(0.5f, 0.45f, 1f),
        new Color(0.95f, 0.65f, 0.35f),
        new Color(0.3f, 0.65f, 0.95f),
        new Color(0.85f, 0.35f, 0.95f),
        new Color(0.45f, 0.8f, 0.85f),
    };

    [Header("Targets")]
    [Tooltip("If empty, uses all MeshRenderers under a child named Ring (case-sensitive).")]
    [SerializeField] private Renderer[] ringRenderers;

    [Header("Look")]
    [SerializeField, Min(0f)] private float emissionScale = 0.35f;

    private PhotonView photonView;

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        if (photonView == null)
            photonView = GetComponentInParent<PhotonView>();

        if (ringRenderers == null || ringRenderers.Length == 0)
            ringRenderers = ResolveDefaultRingRenderers();
    }

    private void Start()
    {
        ApplyColor(ComputeColorForActor(ResolveActorKey()));
    }

    private Renderer[] ResolveDefaultRingRenderers()
    {
        Transform ring = transform.Find("Ring");
        if (ring == null)
            return System.Array.Empty<Renderer>();

        MeshRenderer[] meshes = ring.GetComponentsInChildren<MeshRenderer>(true);
        if (meshes != null && meshes.Length > 0)
            return meshes;

        SkinnedMeshRenderer[] skins = ring.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        return skins != null && skins.Length > 0 ? skins : System.Array.Empty<Renderer>();
    }

    private int ResolveActorKey()
    {
        if (photonView == null)
            return 1;

        if (PhotonNetwork.InRoom && photonView.Owner != null)
            return photonView.Owner.ActorNumber;

        return photonView.ViewID > 0 ? photonView.ViewID : 1;
    }

    private static Color ComputeColorForActor(int actorKey)
    {
        int index = Mathf.Abs(actorKey - 1) % Palette.Length;
        return Palette[index];
    }

    private void ApplyColor(Color color)
    {
        if (ringRenderers == null)
            return;

        for (int i = 0; i < ringRenderers.Length; i++)
        {
            Renderer r = ringRenderers[i];
            if (r == null)
                continue;

            Material[] mats = r.materials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null)
                    continue;

                if (mats[m].HasProperty(BaseColorId))
                    mats[m].SetColor(BaseColorId, color);
                if (mats[m].HasProperty(ColorId))
                    mats[m].SetColor(ColorId, color);

                if (emissionScale > 0f && mats[m].HasProperty(EmissionColorId))
                {
                    Color emission = color * emissionScale;
                    emission.a = 1f;
                    mats[m].SetColor(EmissionColorId, emission);
                    mats[m].EnableKeyword("_EMISSION");
                }
            }

            r.materials = mats;
        }
    }
}
