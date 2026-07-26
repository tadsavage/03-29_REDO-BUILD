using UnityEditor;
using UnityEngine;

namespace GameCore.Inventory.EditorTools
{
    /// <summary>
    /// Adds a "Find Location" button directly under PalletData's Location Name field, jumping the
    /// Scene view to the rack slot this pallet is recorded as living in.
    ///
    /// Drawn field-by-field (rather than DrawDefaultInspector + a button at the bottom) purely so
    /// the button sits immediately beneath the location fields it acts on.
    /// </summary>
    [CustomEditor(typeof(PalletData))]
    public class PalletDataEditor : Editor
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

                // Slot the button in right after the readable location name.
                if (prop.propertyPath == "_locationName")
                    DrawFindLocationButton();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFindLocationButton()
        {
            var pallet = (PalletData)target;
            string address = pallet.LocationName;
            bool hasAddress = !string.IsNullOrWhiteSpace(address);

            using (new EditorGUI.DisabledScope(!hasAddress))
            {
                if (GUILayout.Button(hasAddress ? $"Find Location  ({address})" : "Find Location  (no location recorded)"))
                {
                    var location = InventoryCrossRefEditorUtil.FindLocationByAddress(address);
                    if (location == null)
                        InventoryCrossRefEditorUtil.ReportNotFound("location", address);
                    else
                        InventoryCrossRefEditorUtil.SelectAndFrame(location.gameObject);
                }
            }

            if (!hasAddress)
            {
                EditorGUILayout.HelpBox(
                    "This pallet has no rack slot recorded yet — it is still on the dock or in a trailer. " +
                    "Location Name is set when a Reach Truck puts it away into a rack.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(2);
        }
    }
}
