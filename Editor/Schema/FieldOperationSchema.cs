using System;
using System.Collections;
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
    public class FieldOperationSchema : IInitializable {
        public const string DefaultSubIdentifier = "default";
        [JsonProperty("data")]
        internal Dictionary<string, Dictionary<string, Operation>> assemblyData = new Dictionary<string, Dictionary<string, Operation>>();
        [JsonIgnore]
        public DictionaryView<string, Dictionary<string, Operation>, string, Operation> data;
        public FieldOperationSchema() {
            this.data = new DictionaryView<string, Dictionary<string, Operation>, string, Operation>(() => assemblyData, (old) => old.Values.SelectMany(a => a.Values).GroupBy(a => a.identifier).ToDictionary(a => a.Key, b => new Operation
            {
                identifier = b.Key,
                callVariants = b.SelectMany(c => c.callVariants).ToDictionary(d => d.Key, d => d.Value)
            }).ToDictionary(a => a.Key, b => b.Value));
        }
        public BlobAssetReference<FunctionList<FieldOperation>> CreateOperationsList(Allocator allocator = Allocator.Persistent) {
            var operations = data.SelectMany(operation => operation.Value.callVariants.Select(variant => (operationName: operation.Key, variantName: variant.Key, variant: variant.Value.method.Value))).ToArray();
            Array.Sort(operations, (a, b) =>
            {
                var c1 = a.operationName.CompareTo(b.operationName);
                return c1 != 0 ? c1 : a.variantName.CompareTo(b.variantName);
            });
            return FunctionListUtility.Create<FieldOperation,(string,string,MethodInfo)>(operations, (op) => op.Item3);
        }
        public void OnInit() {
            AssemblyAnalyzer.AnalyzeAssemblies(new FieldOperationFinder() { schema = this });
        }

        [Serializable]
        public class Operation {
            public string identifier;
            public Dictionary<string, Variant> callVariants = new Dictionary<string, Variant>();
            [JsonIgnore]
            public string Name
            {
                get
                {
                    var first = callVariants.FirstOrDefault(c => !string.IsNullOrEmpty(c.Value.displayName));
                    return !string.IsNullOrEmpty(first.Value.displayName) ? first.Value.displayName : identifier;
                }
            }
            [JsonIgnore]
            public int CallVariantCount { get => callVariants.Count; }
            public Variant GetDefaultVariant() => callVariants.FirstOrDefault().Value;
            public string GetDefaultSubIdentifier() => callVariants.FirstOrDefault().Key;
            [Serializable]
            public struct Variant : IEquatable<Variant> {
                public string displayName;
                public SerializableType configType;
                public SerializableMethod method;
                public SerializableType outputType;

                public override bool Equals(object obj) {
                    return obj is Variant call &&
                           configType.Equals(call.configType) &&
                           outputType.Equals(call.outputType) &&
                           EqualityComparer<SerializableMethod>.Default.Equals(this.method, call.method);
                }

                public bool Equals(Variant other) {
                    return configType.Equals(other.configType) &&
                            outputType.Equals(other.outputType) &&
                            EqualityComparer<SerializableMethod>.Default.Equals(this.method, other.method);
                }

                public override int GetHashCode() {
                    int hashCode = -289924153;
                    hashCode = hashCode * -1521134295 + configType.GetHashCode();
                    hashCode = hashCode * -1521134295 + outputType.GetHashCode();
                    hashCode = hashCode * -1521134295 + method.GetHashCode();
                    return hashCode;
                }
            }
        }
    }

}