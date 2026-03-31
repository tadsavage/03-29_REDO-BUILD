using UnityEngine;

public class BuildingHighlighter : MonoBehaviour
{
    [SerializeField]private Material validMaterial;
    [SerializeField]private Material invalidMaterial;
    [SerializeField]private Material deleteMaterial;
    [SerializeField]private Material[] originalMaterials;
    private Renderer[] renderers;
    private SkinnedMeshRenderer[] skinnedMeshRenderers;


    // Cache the original materials of the building so we can revert back to them when needed
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
    // Take in a placement state and decide which material to use based on that - currently just using validMaterial for demonstration purposes
    public void Highlight(bool on)
    {
        int index = 0;
        // later we will pass in a placement state and decide which material to use based on that
        foreach (Renderer r in renderers)
            r.material = on ? validMaterial : originalMaterials[index++];

        foreach (SkinnedMeshRenderer r in skinnedMeshRenderers)
            r.material = on ? validMaterial : originalMaterials[index++];
    }
}
