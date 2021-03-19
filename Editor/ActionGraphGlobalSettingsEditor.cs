using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor {

    [CustomEditor(typeof(ActionGraphGlobalSettings))]
    public class ActionGraphGlobalSettingsEditor : UnityEditor.Editor {
        public override VisualElement CreateInspectorGUI() {
            var rootElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ActionGraphGlobalSettings.Uxml).CloneTree();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(ActionGraphGlobalSettings.Uss);
            rootElement.styleSheets.Add(uss);
            rootElement.Bind(serializedObject);
            return rootElement;
        }
    }
}