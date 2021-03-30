using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NeroWeNeed.ActionGraph.Editor.Graph;
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
            var variables = new Dictionary<string, VariableNodeData>();
            var nodes = new Dictionary<string, GraphNodeSerialiationData>();
            var roots = new List<string>();
            int configLength = 0;
            var actions = definitionAsset.GetActions();
            var nodeIndex = 0;
            foreach (var node in model.nodes) {
                if (node.Value.Data is VariableNodeData variableNodeData) {
                    variables[node.Key] = variableNodeData;
                }
                else if (node.Value.Data is ActionNodeData actionNodeData) {

                    var action = settings.GetAction(definitionAsset.id, actionNodeData.identifier).methods[actionNodeData.subIdentifier];
                    if (action.configType.IsCreated) {
                        var size = UnsafeUtility.SizeOf(action.configType.Value);
                        nodes[node.Key] = new GraphNodeSerialiationData
                        {
                            data = actionNodeData,
                            configOffset = configLength,
                            config = action.configType.Value,
                            configLength = configLength + size,
                            index = nodeIndex++
                        };
                        configLength += size;
                    }
                    else {
                        nodes[node.Key] = new GraphNodeSerialiationData
                        {
                            data = actionNodeData,
                            index = nodeIndex++
                        };
                    }
                    roots.Add(node.Key);
                }
            }
            foreach (var node in nodes) {
                roots.Remove(node.Value.data.Next);
                foreach (var property in node.Value.data.Properties.Values.OfType<INodeReference>()) {
                    roots.Remove(property.Get());
                }
            }
            var builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BlobGraph<TDelegate>>();
            var variableArray = builder.Allocate(ref root.variables, variables.Count);
            var nodeArray = builder.Allocate(ref root.nodes, nodes.Count);
            var variablePathMap = new Dictionary<string, MemoryData>();
            var configTypeMap = new Dictionary<Type, Dictionary<string, MemoryData>>();
            var configData = (IntPtr)UnsafeUtility.Malloc(configLength, 0, Unity.Collections.Allocator.Temp);
            var configOffset = 0;
            foreach (var node in nodes) {
                if (node.Value.HasConfig) {
                    if (!configTypeMap.TryGetValue(node.Value.config, out Dictionary<string, MemoryData> memory)) {
                        memory = new Dictionary<string, MemoryData>();
                        node.Value.config.Decompose((type, fieldInfo, parent, path, parentPath, options) =>
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
                        configTypeMap[node.Value.config] = memory;
                    }
                    foreach (var property in node.Value.data.Properties) {
                        object value;
                        if (property.Value is IFromGraphInstanceType fromGraph) {
                            value = fromGraph.Convert(nodes);
                        }
                        else {
                            value = property.Value;
                        }
                        WriteTypeless(configData, configOffset, value);
                    }
                }
                nodeArray[node.Value.index] = new BlobGraphNode
                {
                    id = actions.FindIndex(a => a.identifier == node.Value.data.identifier && a.subIdentifier == node.Value.data.subIdentifier),
                    configOffset = configOffset,
                    configLength = UnsafeUtility.SizeOf(node.Value.config),
                    next = nodes[node.Value.data.Next].index
                };
                if (node.Value.HasConfig) {
                    configOffset += UnsafeUtility.SizeOf(node.Value.config);
                }
                nodeIndex++;
            }

            definitionAsset.variableType.Value.Decompose((type, fieldInfo, parent, path, parentPath, options) =>
            {
                var data = new MemoryData
                {
                    offset = UnsafeUtility.GetFieldOffset(fieldInfo),
                    length = UnsafeUtility.SizeOf(type)
                };
                if (variablePathMap.TryGetValue(parentPath, out var memory)) {
                    data.offset += memory.offset;
                }
                variablePathMap[path] = data;
                return true;
            });
            var variableIndex = 0;
            foreach (var variable in variables) {
                var node = nodes[variable.Value.Next];
                variableArray[variableIndex++] = new BlobGraphVariable
                {
                    variableOffset = variablePathMap[variable.Value.path].offset,
                    variableLength = variablePathMap[variable.Value.path].length,
                    configOffset = node.configOffset + configTypeMap[node.config][variable.Value.PropertyHint].offset
                };
            }
            var bytes = builder.Allocate(ref root.configuration, configLength);
            var rootArray = builder.Allocate(ref root.roots, roots.Count);
            for (int i = 0; i < roots.Count; i++) {
                rootArray[i] = nodes[roots[i]].index;
            }
            UnsafeUtility.MemCpy(bytes.GetUnsafePtr(), configData.ToPointer(), configLength);
            UnsafeUtility.Free(configData.ToPointer(), Unity.Collections.Allocator.Temp);
        }
        private struct MemoryData {
            public int offset;
            public int length;
        }
        private class GraphNodeSerialiationData : NodeSerialiationData {
            public ActionNodeData data;
        }



        
    }
}