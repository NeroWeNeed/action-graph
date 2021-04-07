using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
namespace NeroWeNeed.ActionGraph {

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public sealed class ActionExecutionSystemGroup : ComponentSystemGroup { }
    public struct ConfigInfo {
        public ConfigDataHandle handle;
        public long length;

        public ConfigInfo(ConfigDataHandle handle, long length) {
            this.handle = handle;
            this.length = length;
        }
    }
    [BurstCompile]
    public unsafe struct ActionExecutionConfigInitJob<TActionDelegate> : IJobEntityBatch where TActionDelegate : Delegate {
        [ReadOnly]
        public ComponentTypeHandle<ActionRequest<TActionDelegate>> requestHandle;
        [WriteOnly]
        public NativeArray<ConfigInfo> handles;
        [BurstCompile]
        public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {

            var requests = batchInChunk.GetNativeArray(requestHandle);
            for (int i = 0; i < requests.Length; i++) {
                var graph = requests[i].value;

                if (graph.IsCreated) {
                    var ptr = UnsafeUtility.Malloc(graph.Value.configuration.Length, 0, Allocator.TempJob);
                    UnsafeUtility.MemCpy(ptr, graph.Value.configuration.GetUnsafePtr(), graph.Value.configuration.Length);
                    handles[i] = new ConfigInfo(new ConfigDataHandle { value = ptr }, graph.Value.configuration.Length);
                }
                else {
                    handles[i] = new ConfigInfo(new ConfigDataHandle { value = IntPtr.Zero.ToPointer() }, 0);
                }
            }
        }
    }
    [BurstCompile]
    public unsafe struct ActionExecutionApplyVariableJob<TActionDelegate, TVariable> : IJobEntityBatch where TActionDelegate : Delegate where TVariable : struct {
        [ReadOnly]
        public ComponentTypeHandle<ActionRequest<TActionDelegate>> requestHandle;


        [ReadOnly]
        public ComponentTypeHandle<ActionVariable<TActionDelegate, TVariable>> variableHandle;

        [ReadOnly]
        public NativeArray<ConfigInfo> handles;
        [BurstCompile]
        public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {

            var requests = batchInChunk.GetNativeArray(requestHandle);
            var variables = batchInChunk.GetNativeArray(variableHandle);
            for (int i = 0; i < requests.Length; i++) {
                var graph = requests[i].value;
                var variable = variables[i].value;
                if (graph.IsCreated) {
                    var variablePointer = UnsafeUtility.AddressOf(ref variable);
                    for (int j = 0; j < graph.Value.variables.Length; j++) {
                        var info = graph.Value.variables[j];
                        var destination = ((IntPtr)handles[i].handle) + info.configOffset;
                        var source = ((IntPtr)variablePointer) + info.variableOffset;
                        UnsafeUtility.MemCpy(destination.ToPointer(), source.ToPointer(), info.variableLength);
                    }
                }
            }
        }
    }
    [BurstCompile]
    public unsafe struct ActionExecutionDoFieldOperations<TActionDelegate> : IJobEntityBatch where TActionDelegate : Delegate {
        [ReadOnly]
        public ComponentTypeHandle<ActionRequest<TActionDelegate>> requestHandle;
        [ReadOnly]
        public NativeArray<ConfigInfo> handles;
        [BurstCompile]
        public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {

            var requests = batchInChunk.GetNativeArray(requestHandle);
            for (int i = 0; i < requests.Length; i++) {
                var graph = requests[i].value;
                if (graph.IsCreated) {
                    for (int j = 0; j < graph.Value.operations.Length; j++) {
                        var config = ((IntPtr)handles[i].handle) + graph.Value.operations[j].configOffset;
                        var destination = ((IntPtr)handles[i].handle) + graph.Value.operations[j].destinationOffset;
                        graph.Value.operations[j].operation.Invoke(config, destination);
                    }
                }
            }
        }
    }


}