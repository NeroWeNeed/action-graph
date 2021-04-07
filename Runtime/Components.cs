using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NeroWeNeed.ActionGraph {

    public struct ActionRequest<TAction> : IComponentData where TAction : Delegate {
        public BlobAssetReference<ActionGraph<TAction>> value;
    }
    public struct ActionRequestAt<TAction> : IComponentData where TAction : Delegate {
        public int startIndex;
    }
    //TODO: Unmanaged?
    public struct ActionVariable<TAction, TVariable> : IComponentData where TVariable : struct where TAction : Delegate {
        public TVariable value;
    }
    public struct ActionResult<TAction, TResult> : IComponentData where TResult : struct where TAction : Delegate {
        public TResult value;
    }
    public interface IFunctionList : IComponentData { }
    public interface IActionList : IFunctionList { }

    public struct Action<TAction> : IBufferElementData where TAction : Delegate {
        public BlobAssetReference<ActionGraph<TAction>> value;
        public static implicit operator Action<TAction>(BlobAssetReference<ActionGraph<TAction>> asset) => new Action<TAction> { value = asset };
    }
    public struct ActionList<TAction> : IActionList where TAction : Delegate {
        public BlobAssetReference<FunctionList<TAction>> value;
        public FunctionPointer<TAction> this[int index]
        {
            get => value.Value.value[index];
        }
        public bool IsCreated => value.IsCreated;
    }
    public struct FieldOperationList : IFunctionList {
        public BlobAssetReference<FunctionList<FieldOperation>> value;
        public FunctionPointer<FieldOperation> this[int index]
        {
            get => value.Value.value[index];
        }
        public bool IsCreated => value.IsCreated;
    }
    public struct FunctionList<TAction> where TAction : Delegate {

        public BlobArray<FunctionPointer<TAction>> value;

    }
}