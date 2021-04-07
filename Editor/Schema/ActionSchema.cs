using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using NeroWeNeed.ActionGraph.Editor.Analyzer;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.AssemblyAnalyzers.Editor;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;
using Unity.Burst;
using Unity.Collections;
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
        private static MethodInfo CreateFunctionListCall = typeof(ActionSchema).GetGenericMethod(nameof(ActionSchema.CreateFunctionList), BindingFlags.Public | BindingFlags.Instance);
        [JsonProperty("data")]
        internal Dictionary<string, Dictionary<string, Action>> assemblyData = new Dictionary<string, Dictionary<string, Action>>();
        [JsonIgnore]
        public DictionaryView<string, Dictionary<string, Action>, Type, Dictionary<string, Action>> data;

        public IEnumerable<Action> this[ActionDefinitionAsset asset]
        {
            get => data[asset.delegateType].Values;
        }
        public Action this[ActionDefinitionAsset asset, string identifier]
        {
            get => data[asset.delegateType][identifier];
        }
        public Action.Variant this[ActionDefinitionAsset asset, string identifier, string subIdentifier]
        {
            get => data[asset.delegateType][identifier].variants[subIdentifier];
        }
        public IEnumerable<Action> this[Type type]
        {
            get => data[type].Values;
        }
        public Action this[Type type, string identifier]
        {
            get => data[type][identifier];
        }
        public Action.Variant this[Type type, string identifier, string subIdentifier]
        {
            get => data[type][identifier].variants[subIdentifier];
        }
        public ActionSchema() {
            this.data = new DictionaryView<string, Dictionary<string, Action>, Type, Dictionary<string, Action>>(() => assemblyData, (old) => old.Values.SelectMany(a => a.Values).GroupBy(a => a.action).ToDictionary(a => a.Key.Value, b => b.ToDictionary(c => c.identifier)));
        }
        internal void UpdateIndices() {
            int index = 0;
            foreach (var entry in data) {
                foreach (var item in entry.Value.SelectMany(kv => kv.Value.variants.Select(variant => (identifier: kv.Key, subIdentifier: variant.Key, variant: variant.Value))).OrderBy((a) => a.identifier + ':' + a.subIdentifier)) {
                    item.variant.index = index++;
                }
                index = 0;
            }
        }
        public void OnInit() {

            AssemblyAnalyzer.AnalyzeAssemblies(new ActionFinder() { schema = this });
        }
        public BlobAssetReference<FunctionList<TAction>> CreateFunctionList<TAction>(Allocator allocator = Allocator.Persistent) where TAction : Delegate {
            var actions = data[typeof(TAction)].SelectMany(action => action.Value.variants.Select(variant => (actionName: action.Key, variantName: variant.Key, variant: variant.Value.call.Value))).OrderBy(a => $"{a.actionName}:{a.variantName}").ToArray();
            return FunctionListUtility.Create<TAction, (string, string, MethodInfo)>(actions, (action) => action.Item3);
        }
        public object CreateFunctionList(Type actionType, Allocator allocator = Allocator.Persistent) {
            return CreateFunctionListCall.MakeGenericMethod(actionType).Invoke(this, new object[] { allocator });
        }
        [Serializable]
        public class Action {
            public string identifier;
            public string displayName;
            public SerializableType action;
            public Dictionary<string, Variant> variants = new Dictionary<string, Variant>();
            [JsonIgnore]
            public int VariantCount { get => variants.Count; }
            public Variant DefaultVariant => variants.TryGetValue(ActionSchema.DefaultSubIdentifier, out var result) ? result : variants.FirstOrDefault().Value;
            public string DefaultSubIdentifier => variants.ContainsKey(ActionSchema.DefaultSubIdentifier) ? ActionSchema.DefaultSubIdentifier : variants.FirstOrDefault().Key;
            [JsonIgnore]
            public string Name { get => string.IsNullOrEmpty(displayName) ? identifier : displayName; }
            [Serializable]
            public class Variant : IEquatable<Variant> {
                public SerializableType config;
                public SerializableMethod call;
                public int index;

                public override bool Equals(object obj) {
                    return obj is Variant method &&
                           config.Equals(method.config) &&
                           EqualityComparer<SerializableMethod>.Default.Equals(this.call, method.call);
                }

                public bool Equals(Variant other) {
                    return config.Equals(other.config) &&
                            EqualityComparer<SerializableMethod>.Default.Equals(this.call, other.call);
                }

                public override int GetHashCode() {
                    int hashCode = -289924153;
                    hashCode = hashCode * -1521134295 + config.GetHashCode();
                    hashCode = hashCode * -1521134295 + call.GetHashCode();
                    return hashCode;
                }
            }
        }
    }

}