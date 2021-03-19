using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Mono.Cecil;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace NeroWeNeed.ActionGraph.Editor {




    [Serializable]
    [ProjectAsset(ProjectAssetType.Json)]
    public class ActionSchema : IInitializationCallback, IDeserializationCallback {
        public const string DefaultSubIdentifier = "default";
        [InitializeOnLoadMethod]
        private static void UpdateSchema() {
            CompilationPipeline.assemblyCompilationFinished -= UpdateSchema;
            CompilationPipeline.assemblyCompilationFinished += UpdateSchema;
        }
        private static void UpdateSchema(string assemblyPath, CompilerMessage[] messages) {
            var asset = ProjectUtility.GetOrCreateProjectAsset<ActionSchema>();
            //if (asset != null) {
            asset.UpdateSchema(assemblyPath);
            ProjectUtility.UpdateProjectAsset(asset);
            //}
        }
        [JsonProperty]
        internal Dictionary<string, AssemblyData> assemblies = new Dictionary<string, AssemblyData>();
        [JsonIgnore]
        public ReadOnlyDictionary<SerializableType, ActionData> actions;

        [JsonIgnore]
        public ActionData this[SerializableType type]
        {
            get => actions[type];
        }
        public void OnInit() {
            foreach (var assemblyPath in Directory.GetFiles("Library/ScriptAssemblies").Where(f => f.EndsWith("dll"))) {
                UpdateSchema(assemblyPath, false);
            }
            UpdateView();
        }
        private void UpdateView() {
            var actionView = new Dictionary<SerializableType, ActionData>();
            foreach (var action in assemblies.SelectMany(assembly => assembly.Value.actions)) {
                if (!actionView.TryGetValue(action.Value.action, out ActionData data)) {
                    data = new ActionData();
                    actionView[action.Value.action] = data;
                }
                data.actions[action.Value.identifier] = action.Value;
            }
            this.actions = new ReadOnlyDictionary<SerializableType, ActionData>(actionView);
        }
        private void UpdateSchema(string assemblyPath, bool updateView = true) {
            using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
            string typeQualifiedName;
            var data = new AssemblyData();
            foreach (var module in assembly.Modules) {
                foreach (var type in module.Types) {
                    if (type.IsAbstract && type.IsSealed) {
                        typeQualifiedName = type.FullName + ", " + assembly.FullName;
                        foreach (var methodDef in type.Methods) {
                            if (methodDef.HasCustomAttributes) {
                                foreach (var attr in methodDef.CustomAttributes.Where(a => a.AttributeType.FullName == typeof(ActionAttribute).FullName)) {

                                    if (!attr.HasConstructorArguments || attr.ConstructorArguments.Count < 2)
                                        continue;
                                    TypeDefinition actionTypeDef = ((TypeReference)attr.ConstructorArguments[0].Value).Resolve();
                                    string identifier = (string)attr.ConstructorArguments[1].Value;
                                    TypeDefinition configTypeDef = attr.ConstructorArguments.Count >= 3 ? ((TypeReference)attr.ConstructorArguments[2].Value).Resolve() : null;
                                    string displayName = attr.ConstructorArguments.Count >= 4 ? (string)attr.ConstructorArguments[3].Value : null;
                                    string subIdentifier = attr.ConstructorArguments.Count == 5 ? (string)attr.ConstructorArguments[4].Value : string.Empty;
                                    if (string.IsNullOrEmpty(subIdentifier)) {
                                        subIdentifier = DefaultSubIdentifier;
                                    }

                                    var actionTypeQualifiedName = actionTypeDef.FullName + ", " + assembly.FullName;
                                    var configTypeQualifiedName = configTypeDef == null ? null : configTypeDef.FullName + ", " + assembly.FullName;
                                    var configType = new SerializableType(configTypeQualifiedName);
                                    var method = new SerializableMethod(typeQualifiedName, methodDef.Name);
                                    var actionMethod = new Action.ActionMethod
                                    {
                                        configType = configType,
                                        method = method
                                    };
                                    if (!data.actions.TryGetValue(identifier, out Action action)) {
                                        action = new Action
                                        {
                                            identifier = identifier,
                                            displayName = displayName,
                                            action = new SerializableType(actionTypeQualifiedName)
                                        };
                                        data.actions[identifier] = action;
                                    }
                                    if (action.methods.ContainsKey(subIdentifier)) {
                                        Debug.LogError($"Duplicate Sub-Identifiers found for identifier '{identifier}': '{subIdentifier}'");
                                    }
                                    action.methods[subIdentifier] = actionMethod;
                                }
                            }

                        }
                    }
                }
            }
            if (data.IsEmpty) {
                assemblies.Remove(assembly.FullName);
            }
            else {
                assemblies[assembly.FullName] = data;
            }
            if (updateView) {
                UpdateView();
            }
        }

        public void OnDeserialize() {
            UpdateView();
        }

        [Serializable]
        public class Action {
            public string identifier;
            public string displayName;
            public SerializableType action;
            public Dictionary<string, ActionMethod> methods = new Dictionary<string, ActionMethod>();
            [JsonIgnore]
            public int MethodCount { get => methods.Count; }
            public ActionMethod GetDefaultMethod() => methods.FirstOrDefault().Value;
            public string GetDefaultSubIdentifier() => methods.FirstOrDefault().Key;
            [JsonIgnore]
            public string Name { get => string.IsNullOrEmpty(displayName) ? identifier : displayName; }
            [Serializable]
            public struct ActionMethod : IEquatable<ActionMethod> {
                public SerializableType configType;
                public SerializableMethod method;

                public override bool Equals(object obj) {
                    return obj is ActionMethod method &&
                           configType.Equals(method.configType) &&
                           EqualityComparer<SerializableMethod>.Default.Equals(this.method, method.method);
                }

                public bool Equals(ActionMethod other) {
                    return configType.Equals(other.configType) &&
                            EqualityComparer<SerializableMethod>.Default.Equals(this.method, other.method);
                }

                public override int GetHashCode() {
                    int hashCode = -289924153;
                    hashCode = hashCode * -1521134295 + configType.GetHashCode();
                    hashCode = hashCode * -1521134295 + method.GetHashCode();
                    return hashCode;
                }
            }
        }

        [Serializable]
        internal class AssemblyData {
            public Dictionary<string, Action> actions = new Dictionary<string, Action>();

            [JsonIgnore]
            public bool IsEmpty { get => actions.Count == 0; }
        }
        public class ActionData {
            public Dictionary<string, Action> actions = new Dictionary<string, Action>();
        }

    }

}