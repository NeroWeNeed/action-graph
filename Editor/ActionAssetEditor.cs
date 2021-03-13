using System;
using NeroWeNeed.ActionGraph.Editor.Graph;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor {
    [CustomEditor(typeof(ActionAsset))]
    public class ActionAssetEditor : UnityEditor.Editor {

        public const string Uxml = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionAsset.uxml";
        public override VisualElement CreateInspectorGUI() {
            var rootElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree();
            rootElement.Q<Button>("open").clicked += () =>
            {
                if (serializedObject?.targetObject is ActionAsset asset) {
                    ActionGraphWindow.ShowWindow(asset);
                }
            };
            return rootElement;
        }
    }
}