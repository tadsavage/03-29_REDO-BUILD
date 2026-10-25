#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Betty > Build Betty. One re-runnable pass: humanoid rig on her FBXs, an Animator Controller with her clips,
/// a placeable prefab (+ ObjDataSO, registry entry, Flavor build-menu slot).
/// </summary>
public static class BettySetup
{
    const string Dir = "Assets/_Project/__Avatar_System2.0/Female/Regular_Body_Type/2. FBX/BettyPages_w_Anims";
    const string ModelPath = Dir + "/BettyTest.fbx";
    const string ControllerPath = Dir + "/Betty.controller";
    const string PrefabPath = "Assets/_Project/Prefabs/Flavor/Betty.prefab";
    const string SoPath = "Assets/_Project/ScriptableObjects/PrefabSOs/Betty.asset";
    const string RatSoPath = "Assets/_Project/ScriptableObjects/PrefabSOs/Rat.asset";
    const string RegistryPath = "Assets/_Project/ScriptableObjects/Obj Data Registry.asset";
    const int BettyId = 940;

    // state name -> (file suffix after "BettyTest@", loop?)
    static readonly (string state, string file, bool loop)[] Clips =
    {
        ("Idle",            "Standing Idle",      true),
        ("CatwalkWalking",  "Catwalk Walking",    true),
        ("CatwalkShow",     "Catwalk Walk",       false),
        ("Samba",           "Samba Dancing",      true),
        ("HipHop",          "Snake Hip Hop Dance",true),
        ("Sitting",         "Sitting",            true),
        ("SittingDisbelief","Sitting Disbelief",  true),
    };

    [MenuItem("Tools/Betty/Build Betty")]
    public static void Build()
    {
        ConfigureRigs();
        var controller = BuildController();
        var prefab = BuildPrefab(controller);
        var so = BuildSO(prefab);
        AddToMenu(so);
        AssetDatabase.SaveAssets();
        Debug.LogWarning("[Betty] Built. Save the scene (Ctrl+S) to keep the build-menu entry. Avatar valid: " + AvatarValid());
    }

    static string ClipPath(string file) => $"{Dir}/BettyTest@{file}.fbx";

    static void ConfigureRigs()
    {
        var main = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        main.animationType = ModelImporterAnimationType.Human;
        main.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        main.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();

        foreach (var c in Clips)
        {
            string path = ClipPath(c.file);
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) { Debug.LogError("[Betty] Missing " + path); continue; }
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            mi.sourceAvatar = avatar;
            var defs = mi.defaultClipAnimations;
            foreach (var d in defs)
            {
                d.loopTime = c.loop; d.loopPose = c.loop;
                bool show = c.state == "CatwalkShow";   // the runway clip keeps its root motion (walk out, turn, walk back); every other clip is in place
                d.lockRootRotation = !show; d.lockRootHeightY = true; d.lockRootPositionXZ = !show;
                d.keepOriginalOrientation = true; d.keepOriginalPositionY = true; d.keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = defs;
            mi.SaveAndReimport();
        }
    }

    static bool AvatarValid()
    {
        var a = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        return a != null && a.isValid && a.isHuman;
    }

    static AnimationClip LoadClip(string file)
    {
        return AssetDatabase.LoadAllAssetsAtPath(ClipPath(file)).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    static AnimatorController BuildController()
    {
        // Rebuilt IN PLACE: deleting the asset gave it a new GUID and the prefab's reference went null.
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = ctrl.layers[0].stateMachine;
        foreach (var old in sm.states.ToArray()) sm.RemoveState(old.state);
        for (int i = 0; i < Clips.Length; i++)
        {
            var clip = LoadClip(Clips[i].file);
            if (clip == null) { Debug.LogError("[Betty] No clip in " + ClipPath(Clips[i].file)); continue; }
            var st = sm.AddState(Clips[i].state, new Vector3(260, 60 * i, 0));
            st.motion = clip;
            if (i == 0) sm.defaultState = st;
        }
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static GameObject BuildPrefab(AnimatorController controller)
    {
        var rat = AssetDatabase.LoadAssetAtPath<ObjDataSO>(RatSoPath);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(rat.prefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Betty";
        for (int i = go.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        // Strip everything Rat-specific; the model child carries the real Animator.
        for (int pass = 0; pass < 3; pass++)
            foreach (var c in go.GetComponents<Component>().ToList())
            {
                if (c == null) continue;
                string n = c.GetType().Name;
                if (n == "RatBehavior" || n == "Rigidbody" || n == "MeshFilter" || n == "MeshRenderer" || n == "AudioSource" || n == "Animator" || n == "LineRenderer" || n == "NavAgentGuidance")
                    Object.DestroyImmediate(c);
            }

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, go.transform);
        visual.name = "Model";
        visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity;
        var anim = visual.GetComponent<Animator>(); if (anim == null) anim = visual.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();

        var box = go.GetComponent<BoxCollider>(); if (box == null) box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.85f, 0f); box.size = new Vector3(0.7f, 1.7f, 0.7f);

        var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (agent == null) agent = go.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agent.agentTypeID = HumanAgentTypeId(); agent.radius = 0.3f; agent.height = 1.7f; agent.speed = 1.1f; agent.acceleration = 4f; agent.angularSpeed = 140f;
        agent.enabled = true;

        var behaviour = go.GetComponent<BettyBehavior>(); if (behaviour == null) behaviour = go.AddComponent<BettyBehavior>();
        var bso = new SerializedObject(behaviour); bso.FindProperty("controller").objectReferenceValue = controller; bso.ApplyModifiedPropertiesWithoutUndo();
        agent.agentTypeID = HumanAgentTypeId();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // Workers use the "Human" NavMesh agent type (the project's default type 0 is "Rat").
    static int HumanAgentTypeId()
    {
        for (int i = 0; i < UnityEngine.AI.NavMesh.GetSettingsCount(); i++)
        {
            var st = UnityEngine.AI.NavMesh.GetSettingsByIndex(i);
            if (UnityEngine.AI.NavMesh.GetSettingsNameFromID(st.agentTypeID) == "Human") return st.agentTypeID;
        }
        Debug.LogError("[Betty] No 'Human' NavMesh agent type found.");
        return 0;
    }

    static ObjDataSO BuildSO(GameObject prefab)
    {
        var rat = AssetDatabase.LoadAssetAtPath<ObjDataSO>(RatSoPath);
        var so = AssetDatabase.LoadAssetAtPath<ObjDataSO>(SoPath);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<ObjDataSO>();
            EditorUtility.CopySerialized(rat, so);
            AssetDatabase.CreateAsset(so, SoPath);
        }
        so.name = "Betty";
        so.id = BettyId;
        so.objName = "Betty Pages";
        so.cost = 0; so.hourlyCost = 0;
        so.category = "Flavor"; so.GL_Line = "Flavor";
        so.prefab = prefab;
        so.footprint = new Vector2Int(1, 1);
        so.customShapeOffsets = new Vector2Int[0];
        EditorUtility.SetDirty(so);

        var registry = AssetDatabase.LoadAssetAtPath<ObjDataRegistry>(RegistryPath);
        if (registry != null && !registry.buttonSOs.Contains(so)) { registry.buttonSOs.Add(so); EditorUtility.SetDirty(registry); }
        return so;
    }

    static void AddToMenu(ObjDataSO so)
    {
        var menu = Object.FindAnyObjectByType<BuildMenuUI>(FindObjectsInactive.Include);
        if (menu == null) { Debug.LogError("[Betty] No BuildMenuUI in the open scene - open Main.unity and run again."); return; }
        var s = new SerializedObject(menu);
        var cats = s.FindProperty("categories");
        for (int i = 0; i < cats.arraySize; i++)
        {
            var cat = cats.GetArrayElementAtIndex(i);
            if (cat.FindPropertyRelative("id").stringValue != "Flavor") continue;
            var list = cat.FindPropertyRelative("items");
            for (int k = 0; k < list.arraySize; k++) if (list.GetArrayElementAtIndex(k).objectReferenceValue == so) { return; }
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = so;
            s.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            return;
        }
        Debug.LogError("[Betty] No 'Flavor' category on the BuildMenuUI.");
    }
}
#endif
