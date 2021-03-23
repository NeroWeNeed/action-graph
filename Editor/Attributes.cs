using System;
using System.Reflection;
using UnityEditor.Experimental.GraphView;

namespace NeroWeNeed.ActionGraph {
    public abstract class NodeLayoutHandler {
        public abstract bool HandleLayout(Type type, FieldInfo fieldInfo, Type actionType, string path, Node node, object value);
    }
    public abstract class NodeLayoutHandler<TValue> : NodeLayoutHandler {

        public abstract bool HandleLayout(Type type, FieldInfo fieldInfo, Type actionType, string path, Node node, TValue value);
        public override bool HandleLayout(Type type, FieldInfo fieldInfo, Type actionType, string path, Node node, object value) => HandleLayout(type, fieldInfo, actionType, path, node, (TValue)value);
    }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class NodeLayoutHandlerAttribute : Attribute {
        public Type value;

        public NodeLayoutHandlerAttribute(Type value) {
            this.value = value;
        }
    }
}