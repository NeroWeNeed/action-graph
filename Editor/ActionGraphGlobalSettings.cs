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
    public class ActionGraphGlobalSettings : ProjectGlobalSettings {
        public const string Uxml = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionGraphGlobalSettings.uxml";
        public const string Uss = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionGraphGlobalSettings.uss";
        public const string DefaultTempDirectory = "Assets/Temp/ActionGraph";
        public const string DefaultArtifactDirectory = "Assets/Resources/Actions";
        public const string DefaultDLLDirectory = "Assets/Libraries";
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
        public string artifactDirectory = DefaultArtifactDirectory;
        public string temporaryDirectory = DefaultTempDirectory;
        public string actionSystemsDLLDirectory = DefaultDLLDirectory;
        public string actionEditorUtilitiesDLLDirectory = DefaultDLLDirectory;
        public List<ActionDefinitionAsset> actions = new List<ActionDefinitionAsset>();
        public ActionAsset CreateTemporaryActionAsset(ActionId actionId) => CreateActionAsset(actionId, $"{temporaryDirectory}/{Guid.NewGuid().ToString("N")}.{Extension}");

        public string CreateArtifactPath() {
            if (!Directory.Exists(artifactDirectory)) {
                Directory.CreateDirectory(artifactDirectory);
            }
            return $"{artifactDirectory}/{Guid.NewGuid().ToString("N")}.bytes";
        }
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
        private void OnValidate() {
            var needsRefresh = false;
            needsRefresh = FixDirectory(ref temporaryDirectory, DefaultTempDirectory) || needsRefresh;
#if ADDRESSABLES_EXISTS
            needsRefresh = FixDirectory(ref artifactDirectory, DefaultAddressablesArtifactDirectory) || needsRefresh;
#else
needsRefresh = FixDirectory(ref artifactDirectory, DefaultArtifactDirectory) || needsRefresh;
#endif
            needsRefresh = FixDirectory(ref actionSystemsDLLDirectory, DefaultDLLDirectory) || needsRefresh;
            needsRefresh = FixDirectory(ref actionEditorUtilitiesDLLDirectory, DefaultDLLDirectory) || needsRefresh;
            if (needsRefresh) {
                EditorUtility.SetDirty(this);
            }
        }
        private bool FixDirectory(ref string directory, string defaultDirectory) {
            var dirty = false;
            if (string.IsNullOrWhiteSpace(directory)) {
                directory = defaultDirectory;
                dirty = true;
            }
            if (directory.EndsWith("/")) {
                directory = directory.Substring(0, directory.Length - 1);
                dirty = true;
            }
            return dirty;
        }
    }
}