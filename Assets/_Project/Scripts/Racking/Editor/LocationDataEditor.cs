using GameCore.Inventory;
using GameCore.Inventory.EditorTools;
using UnityEditor;
using UnityEngine;

namespace GameCore.Racking.EditorTools
{
    /// <summary>
    /// Adds a "Find Pallet" button under LocationData's contents fields, jumping the Scene view to
    /// the pallet this slot claims to hold.
    ///
    /// The point is catching disagreements: a slot can read Occupied while the pallet is physically
    /// somewhere else entirely (mis-placed putaway, stranded on forks, floating). The button either
    /// takes you straight to it, or tells you it isn't in the scene at all — both are useful answers.
    /// </summary>
    [CustomEditor(typeof(LocationData))]
    public class LocationDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                using (new EditorGUI.DisabledScope(prop.propertyPath == "m_Script"))
                    EditorGUILayout.PropertyField(prop, true);

                // Put the button right after the contents block (expiration is the last of them).
                if (prop.propertyPath == "_expirationDate")
                    DrawFindPalletButton();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFindPalletButton()
        {
            var location = (LocationData)target;
            string palletId = location.PalletId;
            string loadId   = location.LoadId;
            bool hasContents = !string.IsNullOrWhiteSpace(palletId) || !string.IsNullOrWhiteSpace(loadId);

            using (new EditorGUI.DisabledScope(!hasContents))
            {
                string label = hasContents
                    ? $"Find Pallet  ({(string.IsNullOrWhiteSpace(loadId) ? palletId : loadId)})"
                    : "Find Pallet  (slot is empty)";

                if (GUILayout.Button(label))
                {
                    var palletGO = InventoryCrossRefEditorUtil.FindPallet(palletId, loadId);
                    if (palletGO == null)
                        InventoryCrossRefEditorUtil.ReportNotFound("pallet", string.IsNullOrWhiteSpace(loadId) ? palletId : loadId);
                    else
                        InventoryCrossRefEditorUtil.SelectAndFrame(palletGO);
                }
            }

            // Flag the exact inconsistency this button exists to investigate.
            if (location.Status == LocationStatus.Occupied && !hasContents)
            {
                EditorGUILayout.HelpBox(
                    "This slot is marked Occupied but has no pallet recorded. Something claimed the " +
                    "slot without writing its contents.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(2);
        }
    }
}
