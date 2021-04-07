using System;
using Unity.Burst;
using Unity.Entities;

namespace NeroWeNeed.ActionGraph {
    public struct ActionGraph<TAction> where TAction : Delegate {
        public BlobArray<ActionGraphNode<TAction>> nodes;
        public BlobArray<ActionGraphFieldOperation> operations;
        public BlobArray<byte> configuration;
        public BlobArray<ActionGraphVariable> variables;
        public BlobArray<int> roots;

    }
    public struct ActionGraphNode<TAction> where TAction : Delegate {
        public FunctionPointer<TAction> action;
        public int configOffset;
        public int configLength;
        public int next;
    }
    public struct ActionGraphFieldOperation {
        public FunctionPointer<FieldOperation> operation;
        public int configOffset;
        public int configLength;
        public int destinationOffset;
        
    }
    public struct ActionGraphVariable {
        public int variableOffset;
        public int variableLength;
        public int configOffset;
    }

}