using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CSharp.RuntimeBinder;
using NeroWeNeed.ActionGraph.Editor.Graph;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons.Editor;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine.Animations;

namespace NeroWeNeed.ActionGraph.Editor {
    public class GraphAssetProducer {
        public void Create(ActionAssetModel model, ActionDefinitionAsset definitionAsset, ActionGraphGlobalSettings settings) {
            typeof(GraphAssetProducer).GetMethod(nameof(Create), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).MakeGenericMethod(definitionAsset.delegateType).Invoke(this, new object[] { model, definitionAsset, settings });
        }
        private unsafe static void Write<TValue>(IntPtr target, int offset, TValue value) where TValue : struct {
            UnsafeUtility.CopyStructureToPtr(ref value, (target + offset).ToPointer());
        }
        private static void WriteTypeless(IntPtr target, int offset, object value) {
            typeof(GraphAssetProducer).GetMethod(nameof(Write), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(value.GetType()).Invoke(null, new object[] { target, offset, value });
        }
        private unsafe void Create<TDelegate>(ActionAssetModel model, ActionDefinitionAsset definitionAsset, ActionGraphGlobalSettings settings) where TDelegate : Delegate {
            FormatData(model, definitionAsset, settings, out var nodes, out var variables, out var roots, out var configLength, out var actionNodeCount, out var fieldOperationNodeCount);
            Type variableType = definitionAsset.variableType.IsCreated ? definitionAsset.variableType.Value : null;
            var memoryMap = GetConfigMemoryMap(nodes, variableType);
            var actions = definitionAsset.GetActions();
            var builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BlobGraph<TDelegate>>();
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
                        WriteTypeless(configData, node.configOffset, value);
                    }
                }
                if (node is GraphNodeSerializationData<ActionNodeData> actionNode) {
                    nodeArray[node.index] = new BlobGraphNode
                    {
                        id = actions.FindIndex(a => a.identifier == actionNode.data.identifier && a.subIdentifier == actionNode.data.subIdentifier),
                        configOffset = node.configOffset,
                        configLength = node.configLength,
                        next = nodes[actionNode.data.Next].index
                    };
                }
                else if (node is GraphNodeSerializationData<FieldOperationNodeData> fieldOperationNode) {
                    var target = nodes[fieldOperationNode.data.Next];
                    fieldOperationArray[fieldOperationNode.index] = new BlobGraphFieldOperationNode
                    {
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
                    variableArray[variableIndex++] = new BlobGraphVariable
                    {
                        variableOffset = memoryMap[variableType][variable.Value.path].offset,
                        variableLength = memoryMap[variableType][variable.Value.path].length,
                        configOffset = target.configOffset + memoryMap[target.config][variable.Value.PropertyHint].offset
                    };
                }
            }
            UnsafeUtility.MemCpy(bytes.GetUnsafePtr(), configData.ToPointer(), configLength);
            UnsafeUtility.Free(configData.ToPointer(), Unity.Collections.Allocator.Temp);
        }
        private Dictionary<Type, Dictionary<string, MemoryData>> GetConfigMemoryMap(Dictionary<string, GraphNodeSerializationData> nodes, Type variableType) {
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
                    return true;
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
                        return true;
                    });
                    memoryMap[node.config] = memory;
                }
            }
            return memoryMap;
        }
        private void FormatData(ActionAssetModel model,
        ActionDefinitionAsset definitionAsset,
         ActionGraphGlobalSettings settings,
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
            var operations = ProjectUtility.GetOrCreateProjectAsset<FieldOperationSchema>();
            foreach (var node in model.nodes) {
                if (node.Value.Data is VariableNodeData variableNodeData) {
                    variables[node.Key] = variableNodeData;
                }
                else if (node.Value.Data is ActionNodeData actionNodeData) {
                    var action = settings.GetAction(definitionAsset.id, actionNodeData.identifier).variants[actionNodeData.subIdentifier];
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