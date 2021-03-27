using System;
using Microsoft.SqlServer.Server;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace NeroWeNeed.ActionGraph {

    [BurstCompile]
    public unsafe struct ActionExecutionSystemConfigHandleCreationJob<TActionDelegate> : IJobEntityBatch where TActionDelegate : Delegate {
        [ReadOnly]
        public ComponentTypeHandle<ActionExecutionRequest<TActionDelegate>> requestHandle;
        [WriteOnly]
        public NativeArray<ValueTuple<ConfigHandle, long>> handles;
        [BurstCompile]
        public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {

            var requests = batchInChunk.GetNativeArray(requestHandle);
            for (int i = 0; i < requests.Length; i++) {
                var graph = requests[i].value;

                if (graph.IsCreated) {
                    var ptr = UnsafeUtility.Malloc(graph.Value.configuration.Length, 0, Allocator.TempJob);
                    UnsafeUtility.MemCpy(ptr, graph.Value.configuration.GetUnsafePtr(), graph.Value.configuration.Length);
                    handles[i] = ValueTuple.Create(new ConfigHandle { value = ptr }, graph.Value.configuration.Length);
                }
                else {
                    handles[i] = ValueTuple.Create(new ConfigHandle { value = IntPtr.Zero.ToPointer() }, 0);
                }
            }
        }
    }
    [BurstCompile]
    public unsafe struct ActionExecutionSystemVariableInitializationJob<TActionDelegate, TVariable> : IJobEntityBatch where TActionDelegate : Delegate where TVariable : struct {
        [ReadOnly]
        public ComponentTypeHandle<ActionExecutionRequest<TActionDelegate>> requestHandle;
        [ReadOnly]
        public NativeArray<TVariable> variables;
        [ReadOnly]
        public NativeArray<ValueTuple<ConfigHandle, long>> handles;
        [BurstCompile]
        public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
            var requests = batchInChunk.GetNativeArray(requestHandle);
            for (int i = 0; i < requests.Length; i++) {
                var graph = requests[i].value;
                var variable = variables[i];
                if (graph.IsCreated) {
                    var variablePointer = UnsafeUtility.AddressOf(ref variable);
                    for (int j = 0; j < graph.Value.variables.Length; j++) {
                        var info = graph.Value.variables[j];
                        var destination = ((IntPtr)handles[i].Item1) + info.configOffset;
                        var source = ((IntPtr)variablePointer) + info.variableOffset;
                        UnsafeUtility.MemCpy(destination.ToPointer(), source.ToPointer(), info.variableLength);
                    }
                }
            }
        }
    }
    [BurstCompile]
    public unsafe struct ActionExecutionJob<TActionDelegate> : IJobEntityBatch where TActionDelegate : Delegate {
        [ReadOnly]
        public ComponentTypeHandle<ActionExecutionRequest<TActionDelegate>> requestHandle;
        [ReadOnly]
        public NativeArray<ValueTuple<ConfigHandle, long>> handles;
        [ReadOnly]
        public NativeArray<int> startIndices;
        [ReadOnly]
        public ActionIndex<TActionDelegate> index;
        [BurstCompile]
        public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
            
            var requests = batchInChunk.GetNativeArray(requestHandle);
            var nodeStack = new NativeQueue<int>(Allocator.Temp);
            for (int i = 0; i < requests.Length; i++) {
                var graph = requests[i].value;
                if (graph.IsCreated) {
                    if (startIndices[i] < 0) {
                        for (int j = 0; j < graph.Value.roots.Length; j++) {
                            nodeStack.Enqueue(graph.Value.roots[j]);
                        }
                    }
                    else {
                        nodeStack.Enqueue(startIndices[i]);
                    }
                    while (!nodeStack.IsEmpty()) {
                        var node = graph.Value.nodes[nodeStack.Dequeue()];
                        var action = index[node.id];

                        /*
                            Execute Action Code
                        */
                        /*
Aggregate return value if present
                        */
                    }
                    nodeStack.Clear();
                }
            }
        }
    }
    public interface IActionSystem : ISystemBase {
        public EntityQuery Query { get; set; }
    }
    [BurstCompile]
    public struct GraphSystem : IActionSystem {
        private EntityQuery query;

        public EntityQuery Query { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void OnCreate(ref SystemState state) {
            query = state.GetEntityQuery(
                ComponentType.ReadOnly<PlaceHolderComponetA>(),
                ComponentType.ReadOnly<PlaceHolderComponetB>()
            );
            state.RequireForUpdate(query);
            state.RequireSingletonForUpdate<PlaceHolderComponetD>();
            
        }

        public void OnDestroy(ref SystemState state) {
        }

        public void OnUpdate(ref SystemState state) {

        }
    }
}