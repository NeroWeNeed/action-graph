using System;
using UnityEngine;


namespace NeroWeNeed.ActionGraph {

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class SingletonArgumentAttribute : Attribute {

    }
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class ActionArgumentAttribute : Attribute {
        public Type action;
        public string parameterName;

        public ActionArgumentAttribute(Type action, string parameterName) {
            this.action = action;
            this.parameterName = parameterName;
        }
    }
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ActionTypeAttribute : Attribute {
        public string name;

        public ActionTypeAttribute(string name) {
            this.name = name;
        }
    }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class GraphInstanceTypeAttribute : Attribute {
        public Type type;

        public GraphInstanceTypeAttribute(Type type) {
            this.type = type;
        }
    }
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class ActionVariable : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ActionAttribute : Attribute {
        public Type type;
        public string identifier;
        public string subIdentifier;
        public Type configType;
        public string displayName;

        public ActionAttribute(Type type, string identifier, Type configType = null, string displayName = null, string subIdentifier = null) {
            this.type = type;
            this.identifier = identifier;
            this.configType = configType;
            this.displayName = displayName;
            this.subIdentifier = string.IsNullOrWhiteSpace(subIdentifier) ? string.Empty : subIdentifier;
        }
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class FieldOperationAttribute : Attribute {
        public string identifier;
        public string subIdentifier;
        public Type configType;
        public string displayName;
        public Type output;
        public FieldOperationAttribute(string identifier, Type configType, string displayName, string subIdentifier,Type output) {
            this.identifier = identifier;
            this.configType = configType;
            this.displayName = displayName;
            this.subIdentifier = string.IsNullOrWhiteSpace(subIdentifier) ? string.Empty : subIdentifier;
            this.output = output;
        }
    }
    [AttributeUsage(AttributeTargets.All)]
    public sealed class NodeLayoutAttribute : Attribute {
        public NodeLayout value;
        public NodeLayoutAttribute(NodeLayout value) {
            this.value = value;
        }
    }

    public enum NodeLayout {
        Input, Output, Extension
    }
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ActionAssetAttribute : Attribute {
        public string name;

        public ActionAssetAttribute(string name) {
            this.name = name;
        }
    }

}