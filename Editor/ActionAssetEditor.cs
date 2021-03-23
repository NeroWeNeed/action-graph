using System;
using System.Linq;
using NeroWeNeed.ActionGraph.Editor.Graph;
using NeroWeNeed.Commons.Editor;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor {
    [CustomEditor(typeof(ActionAsset))]
    public class ActionAssetEditor : UnityEditor.Editor {

        public const string Uxml = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionAsset.uxml";
        public const string Icon = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionAssetIcon.png";
        private static Texture2D assetIcon;
        public static Texture2D AssetIcon
        {
            get
            {
                return assetIcon ??= AssetDatabase.LoadAssetAtPath<Texture2D>(ActionAssetEditor.Icon);
            }
        }
        private ActionGraphGlobalSettings settings;
        [MenuItem("Assets/Create/Actions/Action Asset")]
        public static void CreateAsset() {
            var endName = EndNameEditAction.CreateInstance<ActionAssetEndNameEditAction>();
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, endName, $"{AssetDatabase.GetAssetPath(Selection.activeObject)}/ActionAsset", ActionAssetEditor.AssetIcon, null);
        }
        private void OnEnable() {
            settings = ProjectUtility.GetProjectSettings<ActionGraphGlobalSettings>();
        }
        public override VisualElement CreateInspectorGUI() {
            var rootElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree();
            var openButton = rootElement.Q<Button>("open");
            openButton.clicked += () =>
            {
                if (serializedObject?.targetObject is ActionAsset asset) {
                    ActionGraphWindow.ShowWindow(asset);
                }
            };
            openButton.SetEnabled(((ActionAsset)serializedObject.targetObject).actionId.IsCreated);
            var id = rootElement.Q<ActionIdField>("id");
            if (settings != null) {
                if (serializedObject.targetObject is ActionAsset actionAsset) {
                    try {
                        id.value = actionAsset.actionId;
                    }
                    catch (Exception) {
                        Debug.LogError($"Error setting Action Id. Consider Reimporting {AssetDatabase.GetAssetPath(actionAsset)}");
                    }
                }

                id.RegisterValueChangedCallback(evt =>
                {

                    var previousInfo = evt.previousValue.IsCreated ? settings[evt.previousValue] : null;
                    var self = (ActionIdField)evt.target;
                    var newInfo = evt.newValue.IsCreated ? settings[evt.newValue] : null;
                    var asset = (ActionAsset)serializedObject.targetObject;
                    var model = asset.CreateModel();
                    bool retainNodes = false;
                    if (previousInfo == null || model.nodes.Count == 0 || ConfirmActionTypeChange(previousInfo, newInfo, out retainNodes)) {
                        model.id = evt.newValue;
                        if (!retainNodes) {
                            model.nodes.Clear();
                            model.masterNodeLayout = default;
                        }
                        asset.UpdateAsset(model);
                        openButton.SetEnabled(evt.newValue.IsCreated);
                    }
                    else {
                        self.SetValueWithoutNotify(evt.previousValue);
                    }
                });
            }
            return rootElement;
        }
        private bool ConfirmActionTypeChange(ActionDefinitionAsset previousInfo, ActionDefinitionAsset newInfo, out bool retainNodes) {
            retainNodes = false;
            string message;
            string okMessage;
            if (newInfo == null) {
                message = $"Are you sure you want to clear ${serializedObject.targetObject.name}'s Action Type? This operation will clear this asset's node data.";
                okMessage = "Clear Action Type";

            }
            else if (newInfo.delegateType != previousInfo.delegateType) {
                message = $"Are you sure you want to change ${serializedObject.targetObject.name}'s Action Type from '${previousInfo.Name}' to '${newInfo.Name}'? This operation will clear this asset's node data.";
                okMessage = "Change Action Type";
            }
            else {
                retainNodes = true;
                return true;
            }
            return EditorUtility.DisplayDialog("Change Action Type?", message, okMessage, "Cancel");
        }
    }
    internal class ActionAssetEndNameEditAction : EndNameEditAction {
        public override void Action(int instanceId, string pathName, string resourceFile) {
            var settings = ProjectUtility.GetOrCreateProjectSettings<ActionGraphGlobalSettings>();
            settings.CreateActionAsset(pathName);
        }
    }
}