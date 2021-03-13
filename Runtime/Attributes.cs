using System;

namespace NeroWeNeed.ActionGraph {
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ActionAttribute : Attribute {
        public Type type;
        public string identifier;
        public Type configType;
        public string displayName;

        public ActionAttribute(Type type, string identifier, Type configType = null, string displayName = null) {
            this.type = type;
            this.identifier = identifier;
            this.configType = configType;
            this.displayName = displayName;
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
}