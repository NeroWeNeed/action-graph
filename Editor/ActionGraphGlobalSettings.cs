using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using NeroWeNeed.Commons.Editor.UIToolkit;
using Newtonsoft.Json;
using Unity.Burst;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor {
    public class ActionGraphGlobalSettings : ProjectGlobalSettings, IInitializationCallback, IEnumerable<ActionGraphGlobalSettings.ActionInfo> {
        public const string Uxml = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionGraphGlobalSettings.uxml";
        public const string Uss = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionGraphGlobalSettings.uss";
        public const string DefaultTempDirectory = "Assets/Temp/ActionGraph";
        public const string DefaultArtifactDirectory = "Assets/Resources/Actions";
        public const string DefaultAddressablesArtifactDirectory = "Assets/ResourceData/Actions";
        public const string Extension = "action";

        [SettingsProvider]
        public static SettingsProvider CreateCustomSettingsProvider() {

            return new ProjectGlobalSettingsProvider<ActionGraphGlobalSettings>("Project/ActionGraphSettings", SettingsScope.Project)
            {
                label = "Action Graph Settings",
                uxmlPath = Uxml,
                ussPath = Uss,
                onEnable = OnEnableSettings,
                onDisable = OnDisableSettings
            };
        }
        private static void OnDisableSettings(SerializedObject serializedObject) {
            var obj = serializedObject?.targetObject as ActionGraphGlobalSettings;
            if (obj == null)
                return;
            var names = new HashSet<string>();
            var removed = obj.actions.RemoveAll(action => string.IsNullOrWhiteSpace(action.name) || !names.Add(action.name));
            if (removed > 0) {
                EditorUtility.SetDirty(obj);
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(obj));
            }
        }
        private static void OnEnableSettings(SerializedObject serializedObject, VisualElement rootElement) {
            var actions = rootElement.Q<ManagedListView>("actions");
            actions.AddItem = () =>
            {
                ((ActionGraphGlobalSettings)serializedObject.targetObject).actions.Add(default);
                EditorUtility.SetDirty(serializedObject.targetObject);
            };
            actions.RemoveItem = () =>
            {
                ((ActionGraphGlobalSettings)serializedObject.targetObject).actions.RemoveAt(actions.selectedIndex);
                EditorUtility.SetDirty(serializedObject.targetObject);
            };
        }
        [Delayed]
        public string artifactDirectory = DefaultArtifactDirectory;
        [Delayed]
        public string temporaryDirectory = DefaultTempDirectory;

        public List<ActionInfo> actions = new List<ActionInfo>();


        public ActionSchema.Action GetAction(ActionId actionId, string identifier) {
            var actionInfo = this[actionId];
            return ProjectUtility.GetOrCreateProjectAsset<ActionSchema>().actions[actionInfo.delegateType].actions[identifier];
        }
        public IEnumerable<ActionSchema.Action> GetActions(ActionId actionId) {
            var actionInfo = this[actionId];
            return ProjectUtility.GetOrCreateProjectAsset<ActionSchema>().actions[actionInfo.delegateType].actions.Values;
        }
        public ActionInfo this[ActionId id]
        {
            get => actions.Find(actionInfo => actionInfo.id == id);
        }
        public bool TryGetActionInfo(string name, out ActionInfo actionInfo) {
            actionInfo = default;
            var index = actions.FindIndex(actionInfo => actionInfo.name == name);
            if (index < 0) {
                return false;
            }
            else {
                actionInfo = actions[index];
                return true;
            }
        }
        public ActionAsset CreateTemporaryActionAsset(ActionId actionId) => CreateActionAsset(actionId, $"{temporaryDirectory}/{Guid.NewGuid().ToString("N")}.{Extension}");
        public ActionAsset CreateActionAsset(string path) => CreateActionAsset(actions.Find(i => path.StartsWith(i.associatedDirectory))?.id ?? default, path);
        public ActionAsset CreateActionAsset(ActionId actionId, string path) {
            if (!path.EndsWith($".{Extension}")) {
                path += $".{Extension}";
            }
            var file = new FileInfo(path);
            if (!file.Directory.Exists) {
                file.Directory.Create();
            }
            var model = new ActionAssetModel { id = actionId };
            using (var stream = file.Create()) {
                using (var writer = new StreamWriter(stream)) {
                    var jsonSettings = JsonConvert.DefaultSettings.Invoke();
                    jsonSettings.TypeNameHandling = TypeNameHandling.All;
                    writer.Write(JsonConvert.SerializeObject(model, jsonSettings));
                }
            }
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<ActionAsset>(path);
        }

        public bool TryGetActionInfo(ActionId id, out ActionInfo actionInfo) {
            actionInfo = default;
            var index = actions.FindIndex(actionInfo => actionInfo.id == id);
            if (index < 0) {
                return false;
            }
            else {
                actionInfo = actions[index];
                return true;
            }
        }
        private void OnValidate() {
            var names = new HashSet<string>();
            foreach (var action in actions) {
                if (!names.Add(action.name)) {
                    Debug.LogError($"Duplicate Action Found: {action.name}.");
                }
            }
            if (string.IsNullOrWhiteSpace(temporaryDirectory)) {
                temporaryDirectory = DefaultTempDirectory;
            }
            if (temporaryDirectory.EndsWith("/")) {
                temporaryDirectory = temporaryDirectory.Substring(0, temporaryDirectory.Length - 1);
            }
            if (string.IsNullOrWhiteSpace(artifactDirectory)) {
#if ADDRESSABLES_EXISTS
                artifactDirectory = DefaultAddressablesArtifactDirectory;
#else
                artifactDirectory = DefaultArtifactDirectory;
#endif
            }
            if (artifactDirectory.EndsWith("/")) {
                artifactDirectory = artifactDirectory.Substring(0, artifactDirectory.Length - 1);
            }
            for (int i = 0; i < actions.Count; i++) {
                actions[i]?.Validate();
            }

        }

        public void OnInit() { }

        public IEnumerator<ActionInfo> GetEnumerator() {
            return actions.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return actions.GetEnumerator();
        }

        [Serializable]
        public class ActionInfo {
            [HideInInspector]
            public ActionId id = ActionId.Create();
            [Delayed]
            public string name;
            public string associatedDirectory;
            public string Name { get => string.IsNullOrEmpty(name) ? delegateType.Value?.Name : name; }
            [SuperTypeFilter(typeof(Delegate))]
            [ConcreteTypeFilter]
            public SerializableType delegateType;
            [UnmanagedFilter]
            [ConcreteTypeFilter]
            public SerializableType variableType;
            [SuperTypeFilter(typeof(ActionValidationRule))]
            [ConcreteTypeFilter]
            public SerializableType validatorType;
            internal void Validate() {
                if (string.IsNullOrWhiteSpace(name)) {
                    name = $"Action({id})";
                }
                if (string.IsNullOrWhiteSpace(associatedDirectory)) {
                    associatedDirectory = $"Assets/Actions/{name}";
                }
                if (associatedDirectory.EndsWith("/")) {
                    associatedDirectory = associatedDirectory.Substring(0, associatedDirectory.Length - 1);
                }
            }
        }
    }
}