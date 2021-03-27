using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using NeroWeNeed.ActionGraph.Editor.Analyzer;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.AssemblyAnalyzers.Editor;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.Experimental.GraphView;
using UnityEngine;


namespace NeroWeNeed.ActionGraph.Editor.Schema {
    [Serializable]
    [ProjectAsset(ProjectAssetType.Json)]
    public class ActionSchema : IInitializable {
        public const string DefaultSubIdentifier = "default";
        [JsonProperty("data")]
        internal Dictionary<string, Dictionary<string, Action>> assemblyData = new Dictionary<string, Dictionary<string, Action>>();
        [JsonIgnore]
        public DictionaryView<string, Dictionary<string, Action>, Type, Dictionary<string, Action>> data;
        public ActionSchema() {
            this.data = new DictionaryView<string, Dictionary<string, Action>, Type, Dictionary<string, Action>>(() => assemblyData, (old) => old.Values.SelectMany(a => a.Values).GroupBy(a => a.action).ToDictionary(a => a.Key.Value, b => b.ToDictionary(c => c.identifier)));
        }
        public void OnInit() {
            AssemblyAnalyzer.AnalyzeAssemblies(new ActionFinder() { schema = this });
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
    }

}