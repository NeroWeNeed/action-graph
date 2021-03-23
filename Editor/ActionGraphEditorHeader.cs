using NeroWeNeed.ActionGraph.Editor.Graph;
using NeroWeNeed.Commons.Editor;
using UnityEditor;
using UnityEngine;

namespace NeroWeNeed.ActionGraph.Editor {
    [InitializeOnLoad]
    public static class ActionGraphEditorHeader {
        static ActionGraphEditorHeader() {
            UnityEditor.Editor.finishedDefaultHeaderGUI -= AddHeader;
            UnityEditor.Editor.finishedDefaultHeaderGUI += AddHeader;
        }

        private static void AddHeader(UnityEditor.Editor editor) {
            if (editor?.target != null && editor.targets.Length == 1 && editor.target is ScriptableObject scriptableObject) {
                if (editor.target.GetType().GetSerializableFields(fieldInfo => typeof(ActionAsset).IsAssignableFrom(fieldInfo.FieldType)).Length > 0) {
                    var rect = EditorGUILayout.GetControlRect();
                    if (GUI.Button(rect, "Open In Action Graph")) {
                        ActionGraphWindow.ShowWindow(scriptableObject);
                    }
                }
            }
        }
    }
}