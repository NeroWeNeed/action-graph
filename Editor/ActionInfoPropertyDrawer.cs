using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor {

    //[CustomPropertyDrawer(typeof(ActionGraphGlobalSettings.ActionInfo))]
/*     public class ActionInfoPropertyDrawer : PropertyDrawer {
        public const string Uxml = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionInfo.uxml";
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            var element = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree();
            element.BindProperty(property);
            return element;
        }
                public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
                    return EditorUtility
                    return base.GetPropertyHeight(property, label);
                }

    } */
}