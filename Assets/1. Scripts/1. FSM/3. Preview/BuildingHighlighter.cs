using UnityEngine;

public class BuildingHighlighter : MonoBehaviour
{
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;
    [SerializeField] private Material deleteMaterial;

    private Material[] originalMaterials;
    private Renderer[] renderers;
    private SkinnedMeshRenderer[] skinnedMeshRenderers;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        originalMaterials = new Material[renderers.Length + skinnedMeshRenderers.Length];

        int index = 0;
        foreach (Renderer r in renderers)
            originalMaterials[index++] = r.material;

        foreach (SkinnedMeshRenderer r in skinnedMeshRenderers)
            originalMaterials[index++] = r.material;
    }

    public void HighlightValid(bool on)
    {
        SetMaterial(on ? validMaterial : null);
    }

    public void HighlightInvalid(bool on)
    {
        SetMaterial(on ? invalidMaterial : null);
    }

    public void HighlightDelete(bool on)
    {
        SetMaterial(on ? deleteMaterial : null);
    }

    private void SetMaterial(Material overrideMat)
    {
        int index = 0;

        foreach (Renderer r in renderers)
            r.material = overrideMat ? overrideMat : originalMaterials[index++];

        foreach (SkinnedMeshRenderer r in skinnedMeshRenderers)
            r.material = overrideMat ? overrideMat : originalMaterials[index++];
    }
}
