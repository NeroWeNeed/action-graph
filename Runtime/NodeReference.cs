using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace NeroWeNeed.ActionGraph {

    [GraphInstanceType(typeof(NodeReference))]
    public struct NodeIndex {
        public int value;
    }
    public struct NodeReference : INodeReference, IFromGraphInstanceType<NodeIndex> {
        public string value;

        public NodeReference(string value) {
            this.value = value;
        }
        public NodeIndex Convert<TNodeSerializationData>(Dictionary<string, TNodeSerializationData> nodes) where TNodeSerializationData : NodeSerializationData {
            return new NodeIndex { value = nodes.TryGetValue(value, out var data) ? data.index : -1 };
        }

        public string Get() {
            return value;
        }

        public void Set(string value) {
            this.value = value;
        }

        object IFromGraphInstanceType.Convert<TNodeSerializationData>(Dictionary<string, TNodeSerializationData> nodes) => Convert(nodes);
        public static explicit operator string(NodeReference reference) => reference.value;
        public static explicit operator NodeReference(string value) => new NodeReference(value);
    }
    public interface INodeReference {
        public string Get();
        public void Set(string value);

    }
    public interface IFromGraphInstanceType {
        public object Convert<TNodeSerializationData>(Dictionary<string, TNodeSerializationData> nodes) where TNodeSerializationData : NodeSerializationData;
    }
    public interface IFromGraphInstanceType<TValue> : IFromGraphInstanceType {

        public new TValue Convert<TNodeSerializationData>(Dictionary<string, TNodeSerializationData> nodes) where TNodeSerializationData : NodeSerializationData;
    }
    public abstract class NodeSerializationData {
        public int configOffset;
        public int configLength;
        public Type config;
        public bool HasConfig => configLength > 0;
        public int index;
    }

}