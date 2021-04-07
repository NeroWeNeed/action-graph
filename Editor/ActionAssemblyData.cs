using System;
using System.Collections.Generic;
using System.Linq;
using NeroWeNeed.ActionGraph.Editor.CodeGen;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using UnityEditor;
using UnityEditor.Compilation;

namespace NeroWeNeed.ActionGraph.Editor {
    [ProjectAsset(ProjectAssetType.Json)]
    [Serializable]
    public class ActionAssemblyData {
        [InitializeOnLoadMethod]
        public static void InitializeCallbacks() {
            EditorApplication.playModeStateChanged += RebuildDlls;
            CompilationPipeline.compilationFinished += RebuildDlls;
        }
        public static void RebuildDlls(PlayModeStateChange change) {
            if (change == PlayModeStateChange.ExitingEditMode) {
                RebuildDlls();
            }
        }
        public static void RebuildDlls(object obj) {
            RebuildDlls();
        }
        public static void RebuildDlls() {
            var data = ProjectUtility.GetProjectAsset<ActionAssemblyData>();
            if (data != null) {
                var hash = data.GenerateHash();
                if (data.lastHash != hash) {
                    data.lastHash = hash;
                    ProjectUtility.UpdateProjectAsset(data);
                    ActionSystemProducer.CreateAssembly();
                }
            }
        }

        public Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        public int lastHash;
        public int GenerateHash() {
            var definitions = AssetDatabase.FindAssets($"t:{nameof(ActionDefinitionAsset)}").Select(guid => AssetDatabase.LoadAssetAtPath<ActionDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            var entries = this.entries.Where(entry => definitions.Any(definition => definition.id.guid == entry.Key) && entry.Value.@delegate.IsCreated && (entry.Value.@delegate.Value.GetMethod("Invoke").ReturnType == typeof(void) || (entry.Value.@delegate.Value.GetMethod("Invoke").ReturnType != typeof(void) && entry.Value.aggregator.IsCreated))).ToArray();
            int hash = 0;
            foreach (var entry in entries)
            {
                hash += entry.GetHashCode();
            }
            return hash;
        }
        [Serializable]
        public struct Entry : IEquatable<Entry> {
            public SerializableType @delegate;
            public SerializableType variable;
            public SerializableType destroyEntitiesEntityCommandBufferSystem;
            public bool fieldOperations;
            public SerializableMethod aggregator;

            public Entry(ActionDefinitionAsset asset) {
                @delegate = asset.delegateType;
                variable = asset.variableType;
                destroyEntitiesEntityCommandBufferSystem = asset.destroyEntitiesUsing;
                fieldOperations = asset.useFieldOperations;
                aggregator = asset.aggregator;
            }

            public override bool Equals(object obj) {
                return obj is Entry entry &&
                       @delegate.Equals(entry.@delegate) &&
                       variable.Equals(entry.variable) &&
                       destroyEntitiesEntityCommandBufferSystem.Equals(entry.destroyEntitiesEntityCommandBufferSystem) &&
                       fieldOperations == entry.fieldOperations &&
                       EqualityComparer<SerializableMethod>.Default.Equals(aggregator, entry.aggregator);
            }

            public bool Equals(Entry other) {
                return @delegate.Equals(other.@delegate) &&
                    variable.Equals(other.variable) &&
                    destroyEntitiesEntityCommandBufferSystem.Equals(other.destroyEntitiesEntityCommandBufferSystem) &&
                    fieldOperations == other.fieldOperations &&
                    EqualityComparer<SerializableMethod>.Default.Equals(aggregator, other.aggregator);
            }

            public override int GetHashCode() {
                int hashCode = -26236591;
                hashCode = hashCode * -1521134295 + @delegate.GetHashCode();
                hashCode = hashCode * -1521134295 + variable.GetHashCode();
                hashCode = hashCode * -1521134295 + destroyEntitiesEntityCommandBufferSystem.GetHashCode();
                hashCode = hashCode * -1521134295 + fieldOperations.GetHashCode();
                hashCode = hashCode * -1521134295 + aggregator.GetHashCode();
                return hashCode;
            }
        }
    }
}