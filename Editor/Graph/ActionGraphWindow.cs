using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public class ActionGraphWindow : EditorWindow {
        public const string Uxml = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionGraphEditorWindow.uxml";
        public const string Uss = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionGraphEditorWindow.uss";
        private const string PrefKey = nameof(ActionGraphWindow);
        public static ActionGraphWindow ShowWindow(string title) {
            var window = GetWindow<ActionGraphWindow>();
            window.titleContent = new GUIContent(title);
            window.minSize = new Vector2(640, 480);
            window.Show();
            return window;
        }
        public static ActionGraphWindow ShowWindow(ScriptableObject asset) {
            var window = GetWindow<ActionGraphWindow>();
            window.titleContent = new GUIContent($"Action Graph Editor ({asset.name})");
            window.minSize = new Vector2(640, 480);
            window.Show();
            window.LoadAsset(asset);
            return window;
        }
        [OnOpenAsset(1)]
        public static bool OnOpen(int instanceID, int line) {
            var asset = EditorUtility.InstanceIDToObject(instanceID);
            if (asset is ActionAsset actionGraphAsset) {
                var window = ShowWindow(actionGraphAsset);
                window.LoadAsset(actionGraphAsset);
                return true;
            }
            else {
                return false;
            }
        }
        private void OnEnable() {
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree(this.rootVisualElement);
            this.rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(Uss));
            var graphView = rootVisualElement.Q<ActionGraphView>("graphView");
            graphView.window = this;
            var saveButton = this.rootVisualElement.Q<ToolbarButton>("save");
            saveButton.clicked += () => graphView.SaveModules();
            graphView.RegisterCallback<ActionGraphValidationUpdateEvent>(evt =>
            {
                saveButton.SetEnabled(graphView.IsValid());
            });
            var old = EditorPrefs.GetString(PrefKey,null);
            if (old != null) {
                try {
                    var obj = JsonConvert.DeserializeObject<ContextInfo>(old);
                    graphView.LoadModules(obj.modules, obj.Container);
                }
                catch (Exception e) {
                    Debug.LogError($"Unable to restore ActionGraph state: {e.Message}");
                }
            }

        }

        private void LoadAsset(ScriptableObject asset) {
            var settings = ProjectUtility.GetProjectSettings<ActionGraphGlobalSettings>();
            if (settings != null) {

                var graphView = rootVisualElement.Q<ActionGraphView>("graphView");
                if (asset is ActionAsset actionAsset) {
                    if (ActionDefinitionAsset.Load(actionAsset.actionId,out var definitionAsset)) {
                        graphView.LoadModule(new ActionModule(actionAsset, definitionAsset, definitionAsset.Name));
                        EditorPrefs.SetString(PrefKey, JsonConvert.SerializeObject(graphView.model.context));
                    }
                    else {
                        Debug.LogError($"Unknown Action Type with guid '{actionAsset.actionId}' in {actionAsset.name}");
                    }
                }
                else {
                    var actionModules = new List<ActionModule>();

                    foreach (var fieldInfo in asset.GetType().GetSerializableFields(fieldInfo => typeof(ActionAsset).IsAssignableFrom(fieldInfo.FieldType))) {
                        var attr = fieldInfo.GetCustomAttribute<ActionTypeAttribute>();
                        if (attr != null && ActionDefinitionAsset.Load(attr.type, out var definitionAsset)) {
                            actionModules.Add(new ActionModule((ActionAsset)fieldInfo.GetValue(asset) ?? settings.CreateTemporaryActionAsset(definitionAsset.id),definitionAsset, $"{fieldInfo.Name} [{definitionAsset.Name}]"));
                        }
                    }
                    if (actionModules.Count > 0) {
                        graphView.LoadModules(actionModules, asset);
                        EditorPrefs.SetString(PrefKey, JsonConvert.SerializeObject(graphView.model.context));
                    }
                    else {
                        Debug.LogError("No Action Assets found!");
                    }





                }
            }
        }

    }
}