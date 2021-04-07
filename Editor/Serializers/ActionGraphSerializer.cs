using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NeroWeNeed.ActionGraph.Editor.Graph;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.LowLevel.Unsafe;
using Unity.Entities.Serialization;
using UnityEditor;
#if ADDRESSABLES_EXISTS
using UnityEditor.AddressableAssets;
#endif
using UnityEditor.Compilation;
using UnityEngine;
namespace NeroWeNeed.ActionGraph.Editor {
    public static class ActionGraphSerializer {
        [InitializeOnLoadMethod]
        private static void InitializeCallbacks() {
            CompilationPipeline.compilationFinished += UpdateArtifacts;
        }
        private static void UpdateArtifacts(object obj) {
            var settings = ProjectUtility.GetOrCreateProjectSettings<ActionGraphGlobalSettings>();
            var actionSchema = ProjectUtility.GetOrCreateProjectAsset<ActionSchema>();
            var fieldOperationSchema = ProjectUtility.GetOrCreateProjectAsset<FieldOperationSchema>();
            var definitions = AssetDatabase.FindAssets($"t:{nameof(ActionDefinitionAsset)}").Select(guid => AssetDatabase.LoadAssetAtPath<ActionDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid))).ToDictionary(definition => definition.id);
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(ActionAsset)}")) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var action = AssetDatabase.LoadAssetAtPath<ActionAsset>(path);
                var artifactFile = action.GetArtifactFile(settings);
                var importer = AssetImporter.GetAtPath(path);
                var model = action.CreateModel();
                if (definitions.TryGetValue(action.actionId, out ActionDefinitionAsset definitionAsset) && definitionAsset.delegateType.IsCreated) {
                    using var blob = CreateUntyped(model, actionSchema, fieldOperationSchema, definitionAsset, Allocator.Temp);
                    var newGuid = WriteUntypedArtifact(artifactFile, blob, definitionAsset.delegateType);
                    if (importer.userData != newGuid) {
                        importer.userData = newGuid;
                        importer.SaveAndReimport();
                    }
                }
            }
        }


        public static string WriteUntypedArtifact(string artifactFile, UnsafeUntypedBlobAssetReference blobAssetReference, Type type) {
            return (string)typeof(ActionGraphSerializer).GetGenericMethod(nameof(WriteUntypedArtifact), BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(type).Invoke(null, new object[] { artifactFile, blobAssetReference });
        }
        public static string WriteUntypedArtifact<TAction>(string artifactFile, UnsafeUntypedBlobAssetReference blobAssetReference) where TAction : Delegate {
            var blob = blobAssetReference.Reinterpret<ActionGraph<TAction>>();
            return DoWriteArtifact<TAction>(artifactFile, blob);
        }
        public static string WriteArtifact<TAction>(string artifactFile, ActionSchema actionSchema, FieldOperationSchema operationSchema, ActionAsset actionAsset, ActionDefinitionAsset actionDefinitionAsset) where TAction : Delegate {
            using var blob = Create<TAction>(actionAsset.CreateModel(), actionSchema, operationSchema, actionDefinitionAsset, Unity.Collections.Allocator.Temp);
            return DoWriteArtifact<TAction>(artifactFile, blob);
        }
        private static string DoWriteArtifact<TAction>(string artifactFile, BlobAssetReference<ActionGraph<TAction>> blobAssetReference) where TAction : Delegate {
            using var writer = new StreamBinaryWriter(artifactFile);
            writer.Write(blobAssetReference);
            AssetDatabase.ImportAsset(artifactFile);
#if ADDRESSABLES_EXISTS
            var guid = AssetDatabase.GUIDFromAssetPath(artifactFile).ToString();
            if (AddressableAssetSettingsDefaultObject.SettingsExists) {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings != null) {
                    var group = settings.DefaultGroup;
                    var entry = settings.CreateOrMoveEntry(guid, group, true, true);
                }
            }
            return guid;
#else
            return AssetDatabase.GUIDFromAssetPath(artifactFile).ToString();
#endif

        }
        private unsafe static void WriteMemory<TValue>(IntPtr target, int offset, TValue value) where TValue : struct {
            UnsafeUtility.CopyStructureToPtr(ref value, (target + offset).ToPointer());
        }
        private static void WriteMemory(IntPtr target, int offset, object value) {
            typeof(ActionGraphSerializer).GetGenericMethod(nameof(WriteMemory), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(value.GetType()).Invoke(null, new object[] { target, offset, value });
        }
        public unsafe static UnsafeUntypedBlobAssetReference CreateUntyped(ActionAssetModel model, ActionSchema actionSchema, FieldOperationSchema operationSchema, ActionDefinitionAsset definitionAsset, Allocator allocator = Allocator.Persistent) {
            return (UnsafeUntypedBlobAssetReference)typeof(ActionGraphSerializer).GetGenericMethod(nameof(ActionGraphSerializer.CreateUntyped), BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(definitionAsset.delegateType).Invoke(null, new object[] { model, actionSchema, operationSchema, definitionAsset, Allocator.Temp });
        }
        public unsafe static UnsafeUntypedBlobAssetReference CreateUntyped<TAction>(ActionAssetModel model, ActionSchema actionSchema, FieldOperationSchema operationSchema, ActionDefinitionAsset definitionAsset, Allocator allocator = Allocator.Persistent) where TAction : Delegate {
            var blob = Create<TAction>(model, actionSchema, operationSchema, definitionAsset, allocator);
            return UnsafeUntypedBlobAssetReference.Create(blob);
        }
        public unsafe static BlobAssetReference<ActionGraph<TAction>> Create<TAction>(ActionAssetModel model, ActionSchema actionSchema, FieldOperationSchema operationSchema, ActionDefinitionAsset definitionAsset, Allocator allocator = Allocator.Persistent) where TAction : Delegate {
            FormatData(model, definitionAsset, operationSchema, actionSchema, out var nodes, out var variables, out var roots, out var configLength, out var actionNodeCount, out var fieldOperationNodeCount);
            Type variableType = definitionAsset.variableType.IsCreated ? definitionAsset.variableType.Value : null;
            var memoryMap = GetConfigMemoryMap(nodes, variableType);
            var builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ActionGraph<TAction>>();
            var variableArray = builder.Allocate(ref root.variables, variables.Count);
            var nodeArray = builder.Allocate(ref root.nodes, actionNodeCount);
            var fieldOperationArray = builder.Allocate(ref root.operations, fieldOperationNodeCount);
            var configData = (IntPtr)UnsafeUtility.Malloc(configLength, 0, Unity.Collections.Allocator.Temp);
            foreach (var node in nodes.Values) {
                if (node.HasConfig && node.data is IPropertyContainer propertyContainer) {
                    foreach (var property in propertyContainer.Properties) {
                        object value;
                        if (property.Value is IFromGraphInstanceType fromGraph) {
                            value = fromGraph.Convert(nodes);
                        }
                        else {
                            value = property.Value;
                        }
                        WriteMemory(configData, node.configOffset, value);
                    }
                }
                if (node is GraphNodeSerializationData<ActionNodeData> actionNode) {
                    nodeArray[node.index] = new ActionGraphNode<TAction>
                    {
                        action = BurstCompiler.CompileFunctionPointer((TAction)actionSchema[definitionAsset, actionNode.data.identifier, actionNode.data.subIdentifier].call.Value.CreateDelegate(typeof(TAction))),
                        configOffset = node.configOffset,
                        configLength = node.configLength,
                        next = !string.IsNullOrWhiteSpace(actionNode.data.Next) && nodes.TryGetValue(actionNode.data.Next, out var d) ? d.index : -1
                    };
                }
                else if (node is GraphNodeSerializationData<FieldOperationNodeData> fieldOperationNode) {
                    var target = nodes[fieldOperationNode.data.Next];
                    fieldOperationArray[fieldOperationNode.index] = new ActionGraphFieldOperation
                    {
                        operation = BurstCompiler.CompileFunctionPointer((FieldOperation)operationSchema[fieldOperationNode.data.identifier, fieldOperationNode.data.subIdentifier].method.Value.CreateDelegate(typeof(FieldOperation))),
                        configOffset = node.configOffset,
                        configLength = node.configLength,
                        destinationOffset = target.configOffset + memoryMap[target.config][fieldOperationNode.data.PropertyHint].offset
                    };
                }
                else {
                    throw new Exception("Invalid State");
                }
            }
            var bytes = builder.Allocate(ref root.configuration, configLength);
            var rootArray = builder.Allocate(ref root.roots, roots.Count);
            for (int i = 0; i < roots.Count; i++) {
                rootArray[i] = nodes[roots[i]].index;
            }
            if (variableType != null) {
                var variableIndex = 0;
                foreach (var variable in variables) {
                    var target = nodes[variable.Value.Next];
                    variableArray[variableIndex++] = new ActionGraphVariable
                    {
                        variableOffset = memoryMap[variableType][variable.Value.path].offset,
                        variableLength = memoryMap[variableType][variable.Value.path].length,
                        configOffset = target.configOffset + memoryMap[target.config][variable.Value.PropertyHint].offset
                    };
                }
            }
            UnsafeUtility.MemCpy(bytes.GetUnsafePtr(), configData.ToPointer(), configLength);
            UnsafeUtility.Free(configData.ToPointer(), Unity.Collections.Allocator.Temp);
            var blob = builder.CreateBlobAssetReference<ActionGraph<TAction>>(allocator);
            builder.Dispose();
            return blob;
        }
        private static Dictionary<Type, Dictionary<string, MemoryData>> GetConfigMemoryMap(Dictionary<string, GraphNodeSerializationData> nodes, Type variableType) {
            var memoryMap = new Dictionary<Type, Dictionary<string, MemoryData>>();
            if (variableType != null) {
                var variableMemoryMap = new Dictionary<string, MemoryData>();
                variableType.Decompose((type, fieldInfo, parent, path, parentPath, options) =>
                {
                    var data = new MemoryData
                    {
                        offset = UnsafeUtility.GetFieldOffset(fieldInfo),
                        length = UnsafeUtility.SizeOf(type)
                    };
                    if (variableMemoryMap.TryGetValue(parentPath, out var memory)) {
                        data.offset += memory.offset;
                    }
                    variableMemoryMap[path] = data;
                    return false;
                }, new TypeDecompositionOptions
                {
                    exploreChildren = true
                });
                memoryMap[variableType] = variableMemoryMap;
            }
            foreach (var node in nodes.Values) {
                if (node.HasConfig && !memoryMap.TryGetValue(node.config, out Dictionary<string, MemoryData> memory)) {
                    memory = new Dictionary<string, MemoryData>();
                    node.config.Decompose((type, fieldInfo, parent, path, parentPath, options) =>
                    {
                        var data = new MemoryData
                        {
                            offset = UnsafeUtility.GetFieldOffset(fieldInfo),
                            length = UnsafeUtility.SizeOf(type)
                        };
                        if (memory.TryGetValue(parentPath, out var innerMemory)) {
                            data.offset += innerMemory.offset;
                        }
                        memory[path] = data;
                        return false;
                    }, new TypeDecompositionOptions
                    {
                        exploreChildren = true
                    });
                    memoryMap[node.config] = memory;
                }
            }
            return memoryMap;
        }
        private static void FormatData(ActionAssetModel model,
        ActionDefinitionAsset definitionAsset,
         FieldOperationSchema operations,
         ActionSchema actions,
         out Dictionary<string, GraphNodeSerializationData> nodes,
         out Dictionary<string, VariableNodeData> variables,
         out List<string> roots,
         out int configLength,
         out int actionNodeCount,
         out int fieldOperationNodeCount
         ) {
            variables = new Dictionary<string, VariableNodeData>();
            var fieldOperationNodes = new Dictionary<string, GraphNodeSerializationData<FieldOperationNodeData>>();
            nodes = new Dictionary<string, GraphNodeSerializationData>();
            roots = new List<string>();
            configLength = 0;
            actionNodeCount = 0;
            fieldOperationNodeCount = 0;
            foreach (var node in model.nodes) {
                if (node.Value.Data is VariableNodeData variableNodeData) {
                    variables[node.Key] = variableNodeData;
                }
                else if (node.Value.Data is ActionNodeData actionNodeData) {
                    var action = actions[definitionAsset, actionNodeData.identifier, actionNodeData.subIdentifier];
                    GraphNodeSerializationData<ActionNodeData> nodeSerializationData;
                    if (action.config.IsCreated) {
                        var size = UnsafeUtility.SizeOf(action.config.Value);
                        nodeSerializationData = new GraphNodeSerializationData<ActionNodeData>
                        {
                            data = actionNodeData,
                            configOffset = configLength,
                            config = action.config.Value,
                            configLength = size,
                            index = actionNodeCount++
                        };
                        configLength += size;
                    }
                    else {
                        nodeSerializationData = new GraphNodeSerializationData<ActionNodeData>
                        {
                            data = actionNodeData,
                            index = actionNodeCount++
                        };
                    }
                    nodes[node.Key] = nodeSerializationData;
                    roots.Add(node.Key);
                }
                else if (definitionAsset.useFieldOperations && node.Value.Data is FieldOperationNodeData fieldOperationNodeData) {
                    var operation = operations.data[fieldOperationNodeData.identifier].callVariants[fieldOperationNodeData.subIdentifier];
                    GraphNodeSerializationData<FieldOperationNodeData> nodeSerializationData;
                    if (operation.configType.IsCreated) {
                        var size = UnsafeUtility.SizeOf(operation.configType.Value);
                        nodeSerializationData = new GraphNodeSerializationData<FieldOperationNodeData>
                        {
                            data = fieldOperationNodeData,
                            configOffset = configLength,
                            config = operation.configType.Value,
                            configLength = size,
                            index = fieldOperationNodeCount++
                        };
                        configLength += size;
                    }
                    else {
                        nodeSerializationData = new GraphNodeSerializationData<FieldOperationNodeData>
                        {
                            data = fieldOperationNodeData,
                            index = fieldOperationNodeCount++
                        };
                    }
                    fieldOperationNodes[node.Key] = nodeSerializationData;
                    nodes[node.Key] = nodeSerializationData;
                }
            }
            foreach (var node in nodes.Values.OfType<GraphNodeSerializationData<ActionNodeData>>()) {
                roots.Remove(node.data.Next);
                foreach (var property in node.data.Properties.Values.OfType<INodeReference>()) {
                    roots.Remove(property.Get());
                }
            }
            var sortedNodes = fieldOperationNodes.ToList();
            sortedNodes.Sort(new GraphNodeSerializationDataSorter<FieldOperationNodeData>());
            for (int i = 0; i < sortedNodes.Count; i++) {
                sortedNodes[i].Value.index = i;
            }
        }
        private struct MemoryData {
            public int offset;
            public int length;
        }
        private class GraphNodeSerializationData : NodeSerializationData {
            public NodeData data;
        }
        private class GraphNodeSerializationData<TData> : GraphNodeSerializationData where TData : NodeData {
            new public TData data;
        }
        private class GraphNodeSerializationDataSorter<TData> : IComparer<KeyValuePair<string, GraphNodeSerializationData<TData>>> where TData : NodeData, INodeOutput {
            public int Compare(KeyValuePair<string, GraphNodeSerializationData<TData>> x, KeyValuePair<string, GraphNodeSerializationData<TData>> y) {
                return x.Value.data.Next == y.Key ? -1 : (y.Value.data.Next == x.Key ? 1 : 0);
            }
        }
    }
}