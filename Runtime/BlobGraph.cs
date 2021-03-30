using System;
using Unity.Entities;

namespace NeroWeNeed.ActionGraph {
    public struct BlobGraph<TDelegate> where TDelegate : Delegate {
        public BlobArray<BlobGraphNode> nodes;
        public BlobArray<byte> configuration;
        public BlobArray<BlobGraphVariable> variables;
        public BlobArray<int> roots;
    }
    public struct BlobGraphNode {
        public int id;
        public int configOffset;
        public int configLength;
        public int next;
    }
    public struct BlobGraphVariable {
        public int variableOffset;
        public int variableLength;
        public int configOffset;
    }
    public struct BlobSequence {

    }
}