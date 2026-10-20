using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Cavitoon.Editor
{
    [CustomEditor(typeof(CavitoonCavityRendererFeature))]
    internal sealed class CavitoonCavityRendererFeatureEditor : UnityEditor.Editor
    {
        private static readonly GUIContent ModeLabel = new GUIContent(
            "Mode",
            "Cavitoon Lite supports World mode. Screen mode is available in Cavitoon Pro.");

        private static readonly GUIContent WorldModeLabel = new GUIContent(
            "World",
            "World-space cavity included with Cavitoon Lite.");

        private static readonly GUIContent ScreenModeLabel = new GUIContent(
            "Screen (Pro Version Only)",
            "Screen-space cavity is available in Cavitoon Pro.");

        private static readonly GUIContent BothModeLabel = new GUIContent(
            "Both (Pro Version Only)",
            "Screen-space and World-space cavity is available in Cavitoon Pro.");

        private SerializedProperty m_Settings;
        private SerializedProperty m_Shader;

        private void OnEnable()
        {
            m_Settings = serializedObject.FindProperty("m_Settings");
            m_Shader = serializedObject.FindProperty("m_Shader");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawModeSelector();
            DrawWorldSettings();
            DrawRenderingSettings();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(m_Shader);
            if (m_Shader.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign Hidden/Cavitoon/Cavity so the shader is retained in player builds.",
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawModeSelector()
        {
            Rect row = EditorGUILayout.GetControlRect();
            Rect dropdown = EditorGUI.PrefixLabel(row, ModeLabel);

            if (!EditorGUI.DropdownButton(
                    dropdown,
                    WorldModeLabel,
                    FocusType.Keyboard,
                    EditorStyles.popup))
            {
                return;
            }

            var menu = new GenericMenu();
            menu.AddItem(WorldModeLabel, true, OnWorldModeSelected);
            menu.AddDisabledItem(BothModeLabel);
            menu.AddDisabledItem(ScreenModeLabel);
            menu.DropDown(dropdown);
        }

        private static void OnWorldModeSelected()
        {
            // World is the only runtime mode in Cavitoon Lite.
        }

        private void DrawWorldSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("World Space", EditorStyles.boldLabel);
            DrawSetting("worldRadius");
            DrawSetting("worldRidge");
            DrawSetting("worldValley");
            DrawSetting("worldAttenuation");
            DrawSetting("worldSampleCount");
            DrawSetting("maxWorldPixelRadius");
        }

        private void DrawRenderingSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
            DrawSetting("injectionPoint");
            DrawSetting("showInSceneView");
        }

        private void DrawSetting(string propertyName)
        {
            EditorGUILayout.PropertyField(m_Settings.FindPropertyRelative(propertyName));
        }
    }

    internal static class CavitoonCavityInstaller
    {
        private const string MenuPath = "Tools/Cavitoon/Install Into Selected URP Renderer";
        private const string ShaderName = "Hidden/Cavitoon/Cavity";

        [MenuItem(MenuPath, false, 100)]
        private static void InstallIntoSelectedRenderer()
        {
            if (Selection.activeObject is not UniversalRendererData rendererData)
                return;

            Install(rendererData);
        }

        internal static bool Install(UniversalRendererData rendererData)
        {
            if (rendererData == null)
                return false;

            foreach (ScriptableRendererFeature existingFeature in rendererData.rendererFeatures)
            {
                if (existingFeature is CavitoonCavityRendererFeature)
                {
                    EditorGUIUtility.PingObject(existingFeature);
                    Debug.Log($"{rendererData.name} already contains Cavitoon Cavity.", rendererData);
                    return false;
                }
            }

            var feature = ScriptableObject.CreateInstance<CavitoonCavityRendererFeature>();
            feature.name = "Cavitoon Cavity";
            Undo.RegisterCreatedObjectUndo(feature, "Install Cavitoon Cavity");
            AssetDatabase.AddObjectToAsset(feature, rendererData);

            var featureObject = new SerializedObject(feature);
            featureObject.FindProperty("m_Shader").objectReferenceValue = Shader.Find(ShaderName);
            featureObject.ApplyModifiedPropertiesWithoutUndo();
            feature.Create();

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            var rendererObject = new SerializedObject(rendererData);
            SerializedProperty features = rendererObject.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = rendererObject.FindProperty("m_RendererFeatureMap");

            int index = features.arraySize;
            features.InsertArrayElementAtIndex(index);
            features.GetArrayElementAtIndex(index).objectReferenceValue = feature;

            featureMap.InsertArrayElementAtIndex(featureMap.arraySize);
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
            rendererObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();

            Selection.activeObject = rendererData;
            EditorGUIUtility.PingObject(rendererData);
            Debug.Log($"Installed Cavitoon Cavity into {rendererData.name}.", rendererData);
            return true;
        }

        [MenuItem(MenuPath, true)]
        private static bool CanInstallIntoSelectedRenderer()
        {
            return Selection.activeObject is UniversalRendererData;
        }
    }
}
