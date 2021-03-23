using System;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace NeroWeNeed.ActionGraph {

    [GraphInstanceType(typeof(NodeReference))]
    public struct NodeIndex {
        public int value;
    }
    public struct NodeReference : INodeReference {
        public string value;

        public NodeReference(string value) {
            this.value = value;
        }

        public string Get() {
            return value;
        }

        public void Set(string value) {
            this.value = value;
        }

        public static explicit operator string(NodeReference reference) => reference.value;
        public static explicit operator NodeReference(string value) => new NodeReference(value);
    }
    public interface INodeReference {
        public string Get();
        public void Set(string value);

    }

}