using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NeroWeNeed.ActionGraph {
    public struct PlaceHolderComponetA : IComponentData {
        public double value;
    }
    public struct PlaceHolderComponetB : IComponentData {

    }
    public struct PlaceHolderComponetC : IComponentData {

    }
    public struct PlaceHolderComponetD : IComponentData {

    }
    public struct PlaceHolderComponetE : IComponentData {

    }
    public struct ActionExecutionRequest<TAction> : IComponentData where TAction : Delegate {
        public BlobAssetReference<BlobGraph<TAction>> value;
    }
    public struct ActionExecutionRequestAt<TAction> : IComponentData where TAction : Delegate {
        public int startIndex;
    }
    //TODO: Unmanaged?
    public struct ActionVariable<TAction, TVariable> : IComponentData where TVariable : struct where TAction : Delegate {
        public TVariable value;
    }
    public struct ActionResult<TAction, TResult> : IComponentData where TResult : struct where TAction : Delegate {
        public TResult value;
    }
    public struct ActionIndex<TAction> : IComponentData where TAction : Delegate {
        public BlobArray<FunctionPointer<TAction>> value;
        public FunctionPointer<TAction> this[int index]
        {
            get => value[index];
        }
        public bool IsCreated => value.Length > 0;
    }
}