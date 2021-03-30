using System;
using NeroWeNeed.ActionGraph;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
[assembly: RegisterGenericJobType(typeof(ActionExecutionJob<Action>))]
namespace NeroWeNeed.ActionGraph {

    public struct ConfigInfo {
        public ConfigDataHandle handle;
        public long length;

        public ConfigInfo(ConfigDataHandle handle, long length) {
            this.handle = handle;
            this.length = length;
        }
    }
    [BurstCompile]
    public unsafe struct ActionExecutionSystemConfigHandleCreationJob<TActionDelegate> : IJobEntityBatch where TActionDelegate : Delegate {
        [ReadOnly]
        public ComponentTypeHandle<ActionExecutionRequest<TActionDelegate>> requestHandle;
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
    public unsafe struct ActionExecutionSystemVariableInitializationJob<TActionDelegate, TVariable> : IJobEntityBatch where TActionDelegate : Delegate where TVariable : struct {
        [ReadOnly]
        public ComponentTypeHandle<ActionExecutionRequest<TActionDelegate>> requestHandle;


        [ReadOnly]
        public NativeArray<TVariable> variables;
        [ReadOnly]
        public NativeArray<ConfigInfo> handles;
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
                        var destination = ((IntPtr)handles[i].handle) + info.configOffset;
                        var source = ((IntPtr)variablePointer) + info.variableOffset;
                        UnsafeUtility.MemCpy(destination.ToPointer(), source.ToPointer(), info.variableLength);
                    }
                }
            }
        }
    }
    [BurstCompile]
    public unsafe struct ActionExecutionJobv : IJobEntityBatch {
        [ReadOnly]
        public ComponentTypeHandle<ActionExecutionRequest<Action<ConfigDataHandle, long>>> requestHandle;
        [ReadOnly]
        public EntityTypeHandle entityHandle;
        [ReadOnly]
        public ComponentTypeHandle<PlaceHolderComponetA> p_handle;

        /*
        Other handles
        */
        [ReadOnly]
        public ComponentDataFromEntity<ActionExecutionRequestAt<Action<ConfigDataHandle, long>>> requestAtData;
        [ReadOnly]
        public NativeArray<ConfigInfo> configHandles;
        [ReadOnly]
        public ActionIndex<Func<ConfigDataHandle, double,bool>> index;


        public NativeQueue<int> nodeStack;
        [BurstCompile]
        public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {

            var requests = batchInChunk.GetNativeArray(requestHandle);
            var entities = batchInChunk.GetNativeArray(entityHandle);
            var d1 = batchInChunk.GetNativeArray(p_handle);
            
            for (int i = 0; i < requests.Length; i++) {
                var graph = requests[i].value;
                var handle = configHandles[i].handle;
                var v1 = d1[i].value;
                bool output;
                if (graph.IsCreated) {
                    if (requestAtData.HasComponent(entities[i])) {
                        nodeStack.Enqueue(requestAtData[entities[i]].startIndex);
                    }
                    else {
                        for (int j = 0; j < graph.Value.roots.Length; j++) {
                            nodeStack.Enqueue(graph.Value.roots[j]);
                        }
                    }
                    while (!nodeStack.IsEmpty()) {
                        var node = graph.Value.nodes[nodeStack.Dequeue()];
                        output = index[node.id].Invoke(handle, v1);
                        /*
                            Execute Action Code
                        */
                        /*
                            Aggregate return value if present
                        */
                        if (node.next >= 0) {
                            nodeStack.Enqueue(node.next);
                        }
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
        public EntityTypeHandle entityHandle;
        [ReadOnly]
        public ComponentTypeHandle<PlaceHolderComponetA> p_handle;

        /*
        Other handles
        */
        [ReadOnly]
        public ComponentDataFromEntity<ActionExecutionRequestAt<TActionDelegate>> requestAtData;
        [ReadOnly]
        public NativeArray<ConfigInfo> configHandles;
        [ReadOnly]
        public ActionIndex<TActionDelegate> index;


        public NativeQueue<int> nodeStack;
        [BurstCompile]
        public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {

            var requests = batchInChunk.GetNativeArray(requestHandle);
            var entities = batchInChunk.GetNativeArray(entityHandle);
            for (int i = 0; i < requests.Length; i++) {
                var graph = requests[i].value;
                var handle = configHandles[i];
                var a2 = configHandles[i];
                if (graph.IsCreated) {
                    if (requestAtData.HasComponent(entities[i])) {
                        nodeStack.Enqueue(requestAtData[entities[i]].startIndex);
                    }
                    else {
                        for (int j = 0; j < graph.Value.roots.Length; j++) {
                            nodeStack.Enqueue(graph.Value.roots[j]);
                        }
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
                        if (node.next >= 0) {
                            nodeStack.Enqueue(node.next);
                        }
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
        public FunctionPointer<Action<int>> call;

        public void OnCreate(ref SystemState state) {
            query = state.GetEntityQuery(
                ComponentType.ReadOnly<PlaceHolderComponetA>(),
                ComponentType.ReadOnly<PlaceHolderComponetB>()
            );
            state.RequireForUpdate(query);
            state.RequireSingletonForUpdate<PlaceHolderComponetD>();

        }

        public void OnDestroy(ref SystemState state) {

            int xyz = default;
            call.Invoke(xyz);
            call.Invoke(30);
        }

        public void OnUpdate(ref SystemState state) {

        }
    }
}