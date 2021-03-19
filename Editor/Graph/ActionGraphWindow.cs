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
        private const string EditorPrefKey = nameof(ActionGraphWindow);
        public static ActionGraphWindow ShowWindow(string title) {
            var window = GetWindow<ActionGraphWindow>();
            window.titleContent = new GUIContent(title);
            window.minSize = new Vector2(640, 480);
            window.Show();
            return window;
        }
        public static ActionGraphWindow ShowWindow(ActionAsset asset) {
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


            if (EditorPrefs.HasKey(EditorPrefKey)) {
                var obj = JsonConvert.DeserializeObject<List<ActionModule>>(EditorPrefs.GetString(EditorPrefKey));
                graphView.LoadModules(obj, true);
            }
        }
        private void OnDisable() {
            var graphView = rootVisualElement.Q<ActionGraphView>("graphView");
            if (graphView != null) { }

            EditorPrefs.SetString(EditorPrefKey, JsonConvert.SerializeObject(graphView.modules));
        }
        private void LoadAsset(ScriptableObject asset) {
            if (asset is ActionAsset actionAsset) {
                var graphView = rootVisualElement.Q<ActionGraphView>("graphView");
                graphView.LoadModule(new ActionModule(actionAsset), true);
            }
            else {
                var fields = asset.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(field => field.IsSerializable()).ToList();
                
            }
        }

    }
}