using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(OVRMeshRenderer))]
public class HandVisualMaterialSetter : MonoBehaviour
{
    public Material handMaterial;

    private OVRMeshRenderer ovrMeshRenderer;
    private SkinnedMeshRenderer cachedSkinnedRenderer;
    private bool materialApplied;

    private void Awake()
    {
        ovrMeshRenderer = GetComponent<OVRMeshRenderer>();
    }

    private void LateUpdate()
    {
        if (materialApplied) return;
        if (handMaterial == null || ovrMeshRenderer == null) return;
        if (!ovrMeshRenderer.IsInitialized) return;

        ovrMeshRenderer.SetMaterial(handMaterial);

        if (cachedSkinnedRenderer == null)
            cachedSkinnedRenderer = GetComponent<SkinnedMeshRenderer>();
        if (cachedSkinnedRenderer == null)
            cachedSkinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);

        if (cachedSkinnedRenderer != null)
            cachedSkinnedRenderer.sharedMaterial = handMaterial;

        materialApplied = true;
    }
}
