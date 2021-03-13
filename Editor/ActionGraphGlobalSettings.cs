using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using Unity.Burst;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor {
    //TODO: Asset set to null after scanning.
    public class ActionGraphGlobalSettings : ProjectGlobalSettings, IInit {
        public const string Uxml = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionGraphGlobalSettings.uxml";
        public const string Uss = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionGraphGlobalSettings.uss";

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

        [InitializeOnLoadMethod]
        private static void UpdateActions() {
            CompilationPipeline.assemblyCompilationFinished -= UpdateActions;
            CompilationPipeline.assemblyCompilationFinished += UpdateActions;
        }
        private static void UpdateActions(string assemblyPath, CompilerMessage[] messages) {
            var asset = ProjectUtility.GetSettings<ActionGraphGlobalSettings>();
            if (asset == null)
                return;
            var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
            var actionMap = new Dictionary<string, ActionInfo>();
            var guidMap = new Dictionary<string, string>();
            foreach (var action in asset.actions) {
                action.nodes.RemoveAll(node => !node.method.container.IsCreated || node.method.container.Value.Assembly.FullName == assembly.FullName);
                actionMap[action.type.Value.AssemblyQualifiedName] = action;
            }
            string typeQualifiedName;
            bool settingsDirty = false;
            foreach (var module in assembly.Modules) {
                foreach (var type in module.Types) {
                    if (type.IsAbstract && type.IsSealed) {
                        typeQualifiedName = type.FullName + ", " + assembly.FullName;
                        foreach (var method in type.Methods) {
                            if (method.HasCustomAttributes) {
                                foreach (var attr in method.CustomAttributes.Where(a => a.AttributeType.FullName == typeof(ActionAttribute).FullName)) {
                                    if (!attr.HasConstructorArguments || attr.ConstructorArguments.Count < 2)
                                        continue;
                                    TypeDefinition actionTypeDef = ((TypeReference)(attr.ConstructorArguments[0].Value)).Resolve();
                                    string identifier = (string)attr.ConstructorArguments[1].Value;
                                    TypeDefinition configTypeDef = attr.ConstructorArguments.Count >= 3 ? ((TypeReference)attr.ConstructorArguments[2].Value).Resolve() : null;
                                    string displayName = attr.ConstructorArguments.Count == 4 ? (string)attr.ConstructorArguments[3].Value : null;
                                    var actionTypeQualifiedName = actionTypeDef.FullName + ", " + assembly.FullName;
                                    if (actionMap.TryGetValue(actionTypeQualifiedName, out ActionInfo actionInfo)) {
                                        var configTypeQualifiedName = configTypeDef == null ? null : configTypeDef.FullName + ", " + assembly.FullName;
                                        var actionType = new SerializableType(actionTypeQualifiedName);
                                        var configType = configTypeDef == null ? default : new SerializableType();
                                        settingsDirty = true;
                                        actionInfo.nodes.Add(new ActionInfo.Node
                                        {
                                            identifier = identifier,
                                            method = new SerializableMethod(typeQualifiedName, method.Name),
                                            configType = new SerializableType(configTypeQualifiedName),
                                            displayName = displayName
                                        });
                                    }

                                }
                            }
                        }
                    }
                }
            }
            if (settingsDirty) {
                EditorUtility.SetDirty(asset);
                //AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(asset));
            }
        }
        private static void OnDisableSettings(SerializedObject serializedObject) {
            var obj = serializedObject?.targetObject as ActionGraphGlobalSettings;
            if (obj == null)
                return;
            var names = new HashSet<string>();
            var removed = obj.actions.RemoveAll(action => string.IsNullOrWhiteSpace(action.type.AssemblyQualifiedName) || !names.Add(action.type.AssemblyQualifiedName));
            if (removed > 0) {
                EditorUtility.SetDirty(obj);
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(obj));
            }
        }
        private static void OnEnableSettings(SerializedObject serializedObject, VisualElement rootElement) {
            /*             rootElement.Q<Button>("reload-actions").clicked += () =>
                        {

                        }; */
            rootElement.Q<Button>("add-action").clicked += () =>
            {
                var lv = rootElement.Q<ListView>("actions");
                var property = serializedObject.FindProperty("actions");
                var newIndex = property.arraySize;
                property.arraySize++;
                var newElement = property.GetArrayElementAtIndex(newIndex);
                newElement.FindPropertyRelative("type.assemblyQualifiedName").stringValue = string.Empty;
                newElement.FindPropertyRelative("variableType.assemblyQualifiedName").stringValue = string.Empty;
                serializedObject.ApplyModifiedProperties();
            };


            rootElement.Q<Button>("remove-action").clicked += () =>
            {
                var lv = rootElement.Q<ListView>("actions");
                var prop = serializedObject.FindProperty("actions");
                if (lv.selectedIndex >= 0) {
                    prop.DeleteArrayElementAtIndex(lv.selectedIndex);
                }
                serializedObject.ApplyModifiedProperties();
            };
        }
        public string artifactDirectory = "Assets/Artifacts";
        public bool syncOnReload = true;

        public List<ActionInfo> actions = new List<ActionInfo>();

        public ActionInfo this[Type actionType]
        {
            get => actions.Find(actionInfo => actionInfo.type == actionType);
        }
        public ActionInfo this[ActionType type]
        {
            get => actions.Find(actionInfo => actionInfo.guid == type.guid);
        }
        public bool TryGetAction(Type type, out ActionInfo actionInfo) {
            actionInfo = default;
            var index = actions.FindIndex(actionInfo => actionInfo.type == type);
            if (index < 0) {
                return false;
            }
            else {
                actionInfo = actions[index];
                return true;
            }
        }
        public bool TryGetAction(ActionType type, out ActionInfo actionInfo) {
            actionInfo = default;
            var index = actions.FindIndex(actionInfo => actionInfo.guid == type.guid);
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
                if (!names.Add(action.type.AssemblyQualifiedName)) {
                    Debug.LogError($"Duplicate Action Found: {action.type.AssemblyQualifiedName}. Use different delegates for actions with the same signature.");
                }
            }
        }

        public void Init() { }

        [Serializable]
        public class ActionInfo {
            public string guid = Guid.NewGuid().ToString("N");
            public string name;
            public string Name { get => string.IsNullOrEmpty(name) ? type.Value?.Name : name; }

            public bool allowMultipleRoots;
            [SuperTypeFilter(typeof(Delegate))]
            public SerializableType type;
            [UnmanagedFilter]
            [ConcreteTypeFilter]
            public SerializableType variableType;
            [HideInInspector]
            public List<Node> nodes;
            public Node this[string identifier]
            {
                get => nodes.Find(n => n.identifier == identifier);
            }
            [Serializable]
            public struct Node {
                public string identifier;
                public string displayName;
                public SerializableType configType;
                public SerializableMethod method;
                public string Name { get => string.IsNullOrEmpty(displayName) ? identifier : displayName; }

            }
        }


    }
}