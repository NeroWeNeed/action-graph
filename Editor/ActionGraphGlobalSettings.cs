using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using NeroWeNeed.ActionGraph.Editor.Schema;
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
    public class ActionGraphGlobalSettings : ProjectGlobalSettings, IInitializable, IEnumerable<ActionDefinitionAsset> {
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
        }
        private static void OnEnableSettings(SerializedObject serializedObject, VisualElement rootElement) {
            var actions = rootElement.Q<VisualElement>("actions");
            var target = ((ActionGraphGlobalSettings)serializedObject.targetObject);
            foreach (var action in target.actions) {
                var objField = new ObjectField() { value = action };
                objField.SetEnabled(false);
                actions.Add(objField);
            }
        }
        [Delayed]
        public string artifactDirectory = DefaultArtifactDirectory;
        [Delayed]
        public string temporaryDirectory = DefaultTempDirectory;

        //public List<ActionInfo> actions2 = new List<ActionInfo>();
        public List<ActionDefinitionAsset> actions = new List<ActionDefinitionAsset>();


        public ActionSchema.Action GetAction(ActionId actionId, string identifier) {
            var actionInfo = this[actionId];
            return ProjectUtility.GetOrCreateProjectAsset<ActionSchema>().data[actionInfo.delegateType][identifier];
        }
        public IEnumerable<ActionSchema.Action> GetActions(ActionId actionId) {
            var actionInfo = this[actionId];
            return ProjectUtility.GetOrCreateProjectAsset<ActionSchema>().data[actionInfo.delegateType].Values;
        }
        public ActionDefinitionAsset this[ActionId id]
        {
            get => actions.Find(actionInfo => actionInfo.id == id);
        }

        public bool TryGetActionInfo(string name, out ActionDefinitionAsset actionInfo) {
            actionInfo = default;
            var index = actions.FindIndex(actionInfo => actionInfo.displayName == name);
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

        public bool TryGetActionInfo(ActionId id, out ActionDefinitionAsset actionInfo) {
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
                if (!names.Add(action.displayName)) {
                    Debug.LogError($"Duplicate Action Found: {action.displayName}.");
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

        }

        public void OnInit() { }

        public IEnumerator<ActionDefinitionAsset> GetEnumerator() {
            return actions.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return actions.GetEnumerator();
        }
        private void UpdateActionList() {
            actions = AssetDatabase.FindAssets($"t:{nameof(ActionDefinitionAsset)}").Select(guid => AssetDatabase.LoadAssetAtPath<ActionDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid))).ToList();
            EditorUtility.SetDirty(this);
        }
        private void OnEnable() {
            EditorApplication.projectChanged += UpdateActionList;
        }
        private void OnDisable() {
            EditorApplication.projectChanged -= UpdateActionList;
        }
    }
}