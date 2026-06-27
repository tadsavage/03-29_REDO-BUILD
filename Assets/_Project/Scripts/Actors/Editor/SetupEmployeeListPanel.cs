using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Run once via Tools → Setup EmployeeListPanel to create the EmployeeListPanel
/// GameObject with UIDocument, assign UXML/PanelSettings/template, and wire
/// into UIBootstrapper.
///
/// Safe to re-run — finds existing objects and reapplies settings.
/// </summary>
public static class SetupEmployeeListPanel
{
    const string UXML_PATH          = "Assets/_Project/Scripts/UI_UX/EmployeeUI/EmployeeListPanel.uxml";
    const string ITEM_TEMPLATE_PATH = "Assets/_Project/Scripts/UI_UX/EmployeeUI/EmployeeListItem.uxml";
    const string PANEL_SETTINGS_PATH = "Assets/_Project/Scripts/UI_UX/ToolsWindow/ToolsWindowPanelSettings.asset";

    [MenuItem("Tools/Setup EmployeeListPanel")]
    public static void Run()
    {
        // ── Load assets ───────────────────────────────────────────────────────
        var uxml          = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML_PATH);
        var itemTemplate  = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ITEM_TEMPLATE_PATH);
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PANEL_SETTINGS_PATH);

        if (uxml == null)          { Debug.LogError($"[SetupEmployeeListPanel] UXML not found: {UXML_PATH}"); return; }
        if (panelSettings == null) { Debug.LogError($"[SetupEmployeeListPanel] PanelSettings not found: {PANEL_SETTINGS_PATH}"); return; }
        if (itemTemplate == null)  { Debug.LogWarning($"[SetupEmployeeListPanel] Item template not found: {ITEM_TEMPLATE_PATH}"); }

        // ── Find or create the EmployeeListPanel GameObject ───────────────────
        var go = GameObject.Find("EmployeeListPanel");
        if (go == null)
        {
            go = new GameObject("EmployeeListPanel");
            Undo.RegisterCreatedObjectUndo(go, "Create EmployeeListPanel");
        }

        // ── Ensure UIDocument component ───────────────────────────────────────
        var uidoc = go.GetComponent<UIDocument>();
        if (uidoc == null)
            uidoc = Undo.AddComponent<UIDocument>(go);
        else
            Undo.RecordObject(uidoc, "Configure UIDocument");

        // ── Ensure EmployeeListPanelController component ──────────────────────
        // [RequireComponent(typeof(UIDocument))] ensures UIDocument exists.
        var controller = go.GetComponent<EmployeeListPanelController>();
        if (controller == null)
            controller = Undo.AddComponent<EmployeeListPanelController>(go);
        else
            Undo.RecordObject(controller, "Configure EmployeeListPanelController");

        // ── Configure UIDocument ──────────────────────────────────────────────
        uidoc.visualTreeAsset = uxml;
        uidoc.panelSettings   = panelSettings;

        // ── Assign EmployeeListItem template to controller ────────────────────
        var cso = new SerializedObject(controller);
        var templateProp = cso.FindProperty("_listItemTemplate");
        if (templateProp != null)
            templateProp.objectReferenceValue = itemTemplate;
        cso.ApplyModifiedPropertiesWithoutUndo();

        // ── Wire into UIBootstrapper ──────────────────────────────────────────
        var bootstrapper = Object.FindAnyObjectByType<UIBootstrapper>();
        if (bootstrapper != null)
        {
            Undo.RecordObject(bootstrapper, "Wire EmployeeListPanel to Bootstrapper");
            var bso = new SerializedObject(bootstrapper);
            var empField = bso.FindProperty("_employeeListController");
            if (empField != null)
                empField.objectReferenceValue = controller;
            bso.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[SetupEmployeeListPanel] Wired EmployeeListPanelController into UIBootstrapper.");
        }
        else
        {
            Debug.LogWarning("[SetupEmployeeListPanel] UIBootstrapper not found in scene — wire manually.");
        }

        // ── Save scene as dirty ───────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        // ── Report ────────────────────────────────────────────────────────────
        Debug.Log("[SetupEmployeeListPanel] Done.");
        EditorUtility.DisplayDialog(
            "EmployeeListPanel Setup",
            "Done!\n\n" +
            "• EmployeeListPanel GameObject created\n" +
            "• UIDocument configured with UXML + PanelSettings\n" +
            "• Controller assigned with list item template\n" +
            "• Wired into UIBootstrapper (_employeeListController)\n\n" +
            "Save the scene (Ctrl+S) to persist.",
            "OK");
    }
}
