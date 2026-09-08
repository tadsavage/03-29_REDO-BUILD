using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GameCore.Inventory;

/// <summary>
/// One-time batch tool: generates a simple procedural cardboard-case prefab per SkuData asset,
/// sized exactly to that SKU's CaseWidth/CaseHeight/CaseLength (a unique Mesh per size, since
/// PalletBuilder.GetPrefabDimensions reads size straight off the mesh's own bounds — scaling a
/// shared cube's transform would NOT be picked up). Assigns the result back onto each SkuData's
/// private _prefab field via SerializedObject. Menu: Tools/ObjData/Generate Case Prefabs For SKUs.
/// </summary>
public static class CaseGeneratorTool
{
    private const string PrefabDir = "Assets/_Project/Prefabs/Inventory/Cases";
    private const string MeshDir = "Assets/_Project/Prefabs/Inventory/Cases/Meshes";
    private const string MatDir = "Assets/_Project/Prefabs/Inventory/Cases/Materials";

    private static readonly Color[] BoxColors =
    {
        new Color(0.55f, 0.36f, 0.20f), // classic brown cardboard
        new Color(0.32f, 0.20f, 0.12f), // dark brown
        new Color(0.72f, 0.58f, 0.40f), // tan / kraft
        new Color(0.88f, 0.87f, 0.83f), // white
        new Color(0.65f, 0.55f, 0.45f), // light grey-brown
        new Color(0.78f, 0.68f, 0.52f), // light tan
        new Color(0.85f, 0.76f, 0.60f), // lighter tan
        new Color(0.42f, 0.28f, 0.15f), // chocolate brown
        new Color(0.50f, 0.35f, 0.30f), // burgundy-touched cardboard
        new Color(0.75f, 0.55f, 0.50f), // light red/salmon
    };
    private static readonly Color[] TapeColors =
    {
        new Color(0.80f, 0.74f, 0.55f), // classic tan
        new Color(0.88f, 0.82f, 0.66f), // light tan
        new Color(0.62f, 0.50f, 0.32f), // dark tan
        new Color(0.90f, 0.89f, 0.85f), // white / packing tape
    };
    private static readonly Color LabelColor = new Color(0.95f, 0.95f, 0.92f);

    [MenuItem("Tools/ObjData/Generate Case Prefabs For SKUs")]
    public static void GenerateAll()
    {
        EnsureFolder(PrefabDir);
        EnsureFolder(MeshDir);
        EnsureFolder(MatDir);

        var guids = AssetDatabase.FindAssets("t:SkuData");
        int created = 0, skipped = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var sku = AssetDatabase.LoadAssetAtPath<SkuData>(path);
            if (sku == null) continue;

            GameObject prefab = BuildCasePrefab(sku);
            if (prefab == null) { skipped++; continue; }

            var so = new SerializedObject(sku);
            var prop = so.FindProperty("_prefab");
            prop.objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sku);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CaseGeneratorTool] Generated {created} case prefabs (skipped {skipped}).");
    }

    [MenuItem("Tools/ObjData/Generate Case Prefab For Selected SKU")]
    public static void GenerateOneForSelected()
    {
        var sku = Selection.activeObject as SkuData;
        if (sku == null) { Debug.LogWarning("[CaseGeneratorTool] Select a SkuData asset first."); return; }

        GameObject prefab = BuildCasePrefab(sku);
        if (prefab == null) return;

        var so = new SerializedObject(sku);
        var prop = so.FindProperty("_prefab");
        prop.objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sku);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[CaseGeneratorTool] Generated case prefab for {sku.ItemDescription}.");
    }

    /// <summary>One-time repair: rebinds every SkuData's _prefab field to its CURRENT on-disk case
    /// prefab (matched by the same Cs_{ItemNumber}_{sanitized name} path GenerateAll uses), without
    /// regenerating anything (keeps existing colors/tape/flap jitter). Fixes the exact "pallets have
    /// zero cases" symptom caused by stale fileIDs left over from prior regeneration passes made
    /// before BuildCasePrefab's fileID-stabilization fix above. Safe to re-run any time.</summary>
    [MenuItem("Tools/ObjData/Repair Case Prefab Links")]
    public static void RepairPrefabLinks()
    {
        var guids = AssetDatabase.FindAssets("t:SkuData");
        int fixedCount = 0, alreadyOk = 0, missing = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var sku = AssetDatabase.LoadAssetAtPath<SkuData>(path);
            if (sku == null) continue;

            string safeName = $"Cs_{sku.ItemNumber}_{SanitizeName(sku.ItemDescription)}";
            string prefabPath = $"{PrefabDir}/{safeName}.prefab";
            var current = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (current == null)
            {
                Debug.LogWarning($"[CaseGeneratorTool] Repair: no prefab found at '{prefabPath}' for '{sku.name}' — skipped.");
                missing++;
                continue;
            }

            var so = new SerializedObject(sku);
            var prop = so.FindProperty("_prefab");
            if (prop.objectReferenceValue == current) { alreadyOk++; continue; }

            prop.objectReferenceValue = current;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sku);
            fixedCount++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CaseGeneratorTool] Repair complete: fixed {fixedCount}, already correct {alreadyOk}, missing prefab {missing}.");
    }

    private static GameObject BuildCasePrefab(SkuData sku)
    {
        EnsureFolder(PrefabDir);
        EnsureFolder(MeshDir);
        EnsureFolder(MatDir);

        float w = sku.CaseWidth, h = sku.CaseHeight, l = sku.CaseLength;
        if (w <= 0f || h <= 0f || l <= 0f)
        {
            Debug.LogWarning($"[CaseGeneratorTool] '{sku.name}' has invalid case dimensions ({w},{h},{l}) — skipped.");
            return null;
        }

        var rand = new System.Random(sku.ItemNumber); // deterministic per item

        string safeName = $"Cs_{sku.ItemNumber}_{SanitizeName(sku.ItemDescription)}";
        string meshPath = $"{MeshDir}/{safeName}_Mesh.asset";
        string matPath = $"{MatDir}/{safeName}_Mat.mat";
        string tapeMatPath = $"{MatDir}/{safeName}_Tape.mat";
        string labelMatPath = $"{MatDir}/{safeName}_Label.mat";
        string prefabPath = $"{PrefabDir}/{safeName}.prefab";

        // Re-running the generator overwrites a previous pass — AssetDatabase.CreateAsset errors
        // if something already exists at the path, so clear any prior sub-assets first.
        foreach (var p in new[] { meshPath, matPath, tapeMatPath, labelMatPath, prefabPath })
            if (AssetDatabase.LoadAssetAtPath<Object>(p) != null) AssetDatabase.DeleteAsset(p);

        Mesh mesh = BuildBoxMesh(w, h, l);
        mesh.name = safeName + "_Mesh";
        AssetDatabase.CreateAsset(mesh, meshPath);

        Color baseColor = BoxColors[rand.Next(BoxColors.Length)];
        Color jittered = Jitter(baseColor, rand, 0.05f);
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = safeName + "_Mat" };
        mat.color = jittered;
        mat.SetFloat("_Smoothness", 0.12f);
        AssetDatabase.CreateAsset(mat, matPath);

        GameObject root = new GameObject(safeName);
        var mf = root.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = root.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        // Every case gets sealed with varied tape patterns: main strip down center, with 3 patterns:
        // 1) Main strip only (30%)
        // 2) Main strip + crossbars at ends (55%) — reads as 4 flaps folded and sealed
        // 3) Main strip + corner tape (15%) — double-sealed look for reinforced cases
        // Tape sits almost flush on the surface with subtle color jitter for variety.
        const float tapeThickness = 0.0015f;
        const float tapeOffset = tapeThickness / 2f + 0.0002f;

        Material tapeMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = safeName + "_Tape" };
        tapeMat.color = Jitter(TapeColors[rand.Next(TapeColors.Length)], rand, 0.04f);
        AssetDatabase.CreateAsset(tapeMat, tapeMatPath);

        // Main tape strip down center of top
        GameObject mainTape = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(mainTape.GetComponent<Collider>());
        mainTape.name = "Tape_Main";
        mainTape.transform.SetParent(root.transform, false);
        mainTape.transform.localScale = new Vector3(Mathf.Max(0.02f, w * 0.16f), tapeThickness, l * 1.001f);
        mainTape.transform.localPosition = new Vector3(0f, h + tapeOffset, 0f);
        mainTape.GetComponent<MeshRenderer>().sharedMaterial = tapeMat;

        double tapePattern = rand.NextDouble();
        if (tapePattern < 0.55)
        {
            // Pattern 1: Main + crossbars at ends (flap seams)
            float crossbarLen = Mathf.Min(w * 0.55f, w - 0.01f);
            float crossbarInset = Mathf.Min(l * 0.12f, 0.05f);
            foreach (float zSign in new[] { -1f, 1f })
            {
                GameObject crossbar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(crossbar.GetComponent<Collider>());
                crossbar.name = "Tape_Flap";
                crossbar.transform.SetParent(root.transform, false);
                crossbar.transform.localScale = new Vector3(Mathf.Max(0.02f, crossbarLen), tapeThickness, w * 0.16f);
                crossbar.transform.localPosition = new Vector3(0f, h + tapeOffset, zSign * (l / 2f - crossbarInset));
                crossbar.GetComponent<MeshRenderer>().sharedMaterial = tapeMat;
            }
        }
        else if (tapePattern < 0.70)
        {
            // Pattern 2: Main + corner tape for reinforcement (vertical edges)
            float cornerInset = Mathf.Min(w * 0.08f, 0.04f);
            foreach (float xSign in new[] { -1f, 1f })
            {
                GameObject cornerTape = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cornerTape.GetComponent<Collider>());
                cornerTape.name = "Tape_Corner";
                cornerTape.transform.SetParent(root.transform, false);
                cornerTape.transform.localScale = new Vector3(w * 0.1f, tapeThickness, Mathf.Max(0.02f, l * 0.18f));
                cornerTape.transform.localPosition = new Vector3(xSign * (w / 2f - cornerInset), h + tapeOffset, 0f);
                cornerTape.GetComponent<MeshRenderer>().sharedMaterial = tapeMat;
            }
        }
        // else: Main strip only (30% — no additional tape)

        // Variety pass: every case gets labels — they pop out slightly for visual interest.
        // Guarantees no plain unmarked box slips through (per Tad, 2026-07-05).
        Material labelMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = safeName + "_Label" };
        labelMat.color = Jitter(LabelColor, rand, 0.03f);
        AssetDatabase.CreateAsset(labelMat, labelMatPath);

        const float labelPopOut = 0.06f; // Labels pop out 0.06 units for visual interest

        if (rand.NextDouble() < 0.5)
        {
            // Front label(s) — 50% get one front label, 50% get paired side labels
            float labelW = Mathf.Min(l, w) * 0.45f;
            float labelH = h * 0.35f;

            GameObject label = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(label.GetComponent<Collider>());
            label.name = "Label_Front";
            label.transform.SetParent(root.transform, false);
            label.transform.localScale = new Vector3(labelW, labelH, 1f);
            label.transform.localPosition = new Vector3(0f, h / 2f, l / 2f + labelPopOut);
            label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            label.GetComponent<MeshRenderer>().sharedMaterial = labelMat;
        }
        else
        {
            // Paired side labels — one on +X, one on -X for symmetry
            float stripeW = Mathf.Min(w, l) * 0.45f;
            float stripeH = h * 0.35f;

            foreach (float xSign in new[] { 1f, -1f })
            {
                GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(stripe.GetComponent<Collider>());
                stripe.name = $"Label_Side_{(xSign > 0f ? "Right" : "Left")}";
                stripe.transform.SetParent(root.transform, false);
                stripe.transform.localScale = new Vector3(stripeW, stripeH, 1f);
                stripe.transform.localPosition = new Vector3(xSign * (w / 2f + labelPopOut), h / 2f, 0f);
                stripe.transform.localRotation = Quaternion.Euler(0f, xSign > 0f ? -90f : 90f, 0f);
                stripe.GetComponent<MeshRenderer>().sharedMaterial = labelMat;
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool ok);
        Object.DestroyImmediate(root);
        if (!ok) return null;

        // Do NOT return the in-memory `savedPrefab` reference here. SaveAsPrefabAsset hands back an
        // object whose root fileID can be silently RENUMBERED the next time AssetDatabase.Refresh()
        // runs a full reimport pass (e.g. GenerateAll()'s own end-of-batch Refresh(), or any refresh
        // triggered elsewhere) — this exact bug already broke all 30 SkuData->prefab links once this
        // session (2026-07-03/04) and, because this method still returned the stale in-memory
        // reference, it broke them AGAIN on every later regeneration pass (tape/color/ground fixes),
        // which is what caused pallets to build with zero cases (PalletBuilder.Build() silently
        // bailing on a "missing" case prefab) — Tad reported 2026-07-05. Forcing a synchronous
        // import of just this one asset NOW, then reloading it fresh, guarantees the reference
        // handed back (and written onto the SkuData) is the post-stabilization one that survives
        // any later Refresh() elsewhere in the batch.
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    /// <summary>Clones Unity's built-in cube mesh and rescales its vertices directly (not the
    /// transform) to exact half-extents, so the resulting asset's own bounds are correct — this is
    /// what PalletBuilder.GetPrefabDimensions actually reads. Reusing the built-in cube's topology
    /// (24 verts, pre-split per face) avoids hand-rolling triangle winding/normals by hand.
    ///
    /// Ground-based on Y (spans 0..h), centered on X/Z (spans -w/2..w/2, -l/2..l/2) — matches
    /// PalletBuilder.Build()'s own convention: it positions each case layer's transform ORIGIN at
    /// the bottom of that layer (yPos = palletDim.y + h*(caseDim.y+gap)), so a case whose mesh is
    /// centered on its own origin (Unity's default cube is ±0.5 before scaling) would sit with its
    /// bottom half sunk h/2 below where it's supposed to rest — exactly the "cases sinking through
    /// the pallet/ground, top pallet's deck buried in the stack below it" bug Tad reported
    /// 2026-07-05. Shifting Y by +h/2 after scaling fixes it without touching PalletBuilder itself
    /// (X/Z stay centered since CalculateBestLayer already positions cases by their center X/Z).</summary>
    private static Mesh BuildBoxMesh(float w, float h, float l)
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh source = temp.GetComponent<MeshFilter>().sharedMesh;
        Mesh newMesh = Object.Instantiate(source);

        Vector3[] verts = newMesh.vertices;
        for (int i = 0; i < verts.Length; i++)
            verts[i] = new Vector3(verts[i].x * w, verts[i].y * h + h / 2f, verts[i].z * l);
        newMesh.vertices = verts;
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();
        newMesh.RecalculateTangents();

        Object.DestroyImmediate(temp);
        return newMesh;
    }

    private static Color Jitter(Color c, System.Random rand, float amount)
    {
        float J() => (float)(rand.NextDouble() * 2.0 - 1.0) * amount;
        return new Color(
            Mathf.Clamp01(c.r + J()),
            Mathf.Clamp01(c.g + J()),
            Mathf.Clamp01(c.b + J()));
    }

    private static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Item";
        foreach (char ch in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(ch.ToString(), "_");
        return name.Trim().Replace(" ", "");
    }

    private static void EnsureFolder(string path)
    {
        path = path.Replace("\\", "/").TrimEnd('/');
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "Assets";
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
