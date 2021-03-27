using System;
using Unity.Entities;

namespace NeroWeNeed.ActionGraph {
    public struct BlobGraph<TDelegate> where TDelegate : Delegate {
        public BlobArray<Node> nodes;
        public BlobArray<byte> configuration;
        public BlobArray<Variable> variables;
        public BlobArray<int> roots;
        public struct Node {
            public int id;
            public int configOffset;
            public int configLength;
            public int next;
        }
        public struct Variable {
            public int variableOffset;
            public int variableLength;
            public int configOffset;
        }
    }
    public struct BlobSequence {

    }
}