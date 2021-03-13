using System;
using System.Collections.Generic;
using System.Reflection;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public class ActionGraphModel {
        public Dictionary<string, NodeData> nodes = new Dictionary<string, NodeData>();
        public Dictionary<string, ActionInfo> actionInfo = new Dictionary<string, ActionInfo>();
        public TData GetData<TData>(string guid) where TData : NodeData => (TData)nodes[guid];
        public NodeData this[string guid]
        {
            get => nodes[guid];
            set => nodes[guid] = value;
        }
        public void Clear() {
            this.nodes.Clear();
            this.actionInfo.Clear();
        }
        public abstract class NodeData {
            public ActionType actionType;
            public string next;
            [JsonIgnore]
            public string moduleHint;
            public abstract ActionModel.Node ToModelNode(Rect layout);
            public abstract GraphElement CreateNode(ActionGraphView graphView, ActionGraphGlobalSettings settings, Rect layout, string guid);
        }
        public class ActionNodeData : NodeData {
            public string identifier;
            public Dictionary<string, object> properties = new Dictionary<string, object>();

            public override ActionModel.Node ToModelNode(Rect layout) => new ActionModel.Node<ActionNodeData>(this, layout);
            public override GraphElement CreateNode(ActionGraphView graphView, ActionGraphGlobalSettings settings, Rect layout, string guid = null) {
                var info = settings[actionType];
                var nodeInfo = info[identifier];
                if (string.IsNullOrEmpty(guid))
                    guid = Guid.NewGuid().ToString("N");
                var node = new Node()
                {
                    viewDataKey = guid,
                    title = nodeInfo.Name
                };
                var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, ActionGraphView.CreateActionPortType(info.type));
                inputPort.portName = "in";
                inputPort.AddToClassList(ActionGraphView.NodePortClassName);
                inputPort.AddToClassList(ActionGraphView.NodeInputPortClassName);
                inputPort.AddToClassList(ActionGraphView.CollectablePortClassName);
                node.inputContainer.Add(inputPort);
                var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, ActionGraphView.CreateActionPortType(info.type));
                outputPort.portName = "out";
                outputPort.AddToClassList(ActionGraphView.NodePortClassName);
                outputPort.AddToClassList(ActionGraphView.NodeOutputPortClassName);
                outputPort.AddToClassList(ActionGraphView.OutputPortClassName);
                outputPort.RegisterCallback<PortUpdateEvent>(evt =>
                {
                    var self = (Port)evt.currentTarget;
                    var graphView = self.GetFirstAncestorOfType<ActionGraphView>();
                    if (graphView == null)
                        return;
                    var other = evt.portA == self ? evt.portB : evt.portA;
                    switch (evt.type) {
                        case PortUpdateEventType.Connected:
                            graphView.model[self.node.viewDataKey].next = other.node.viewDataKey;
                            break;
                        case PortUpdateEventType.Disconnected:
                            if (graphView.model[self.node.viewDataKey].next == other.viewDataKey)
                                graphView.model[self.node.viewDataKey].next = null;
                            break;
                    }
                });
                node.outputContainer.Add(outputPort);
                if (nodeInfo.configType.IsCreated) {
                    var schema = ProjectUtility.GetOrCreateSingleton<TypeFieldSchema>();
                    var context = new NodeTypeFieldProviderContext(node, info.type);
                    nodeInfo.configType.Value.Decompose((Type type, FieldInfo fieldInfo, string path, TypeDecompositionOptions _) =>
                    {
                        if (schema.CreateField(type, fieldInfo, properties.TryGetValue(path, out object value) ? Convert.ChangeType(value, type) : Activator.CreateInstance(type), out BindableElement element)) {
                            element.bindingPath = path;
                            context.HandleField(fieldInfo.FieldType, fieldInfo, element);
                            return true;
                        }
                        else {
                            return false;
                        }
                    }, new TypeDecompositionOptions
                    {
                        exploreChildren = false

                    });
                }
                node.RegisterCallback<FieldUpdateEvent>(evt =>
                {
                    var element = (Node)evt.currentTarget;
                    var graphView = element.GetFirstAncestorOfType<ActionGraphView>();
                    if (graphView == null)
                        return;
                    graphView.model.GetData<ActionGraphModel.ActionNodeData>(element.viewDataKey).properties[evt.path] = evt.value;
                });
                node.RefreshExpandedState();
                node.SetPosition(layout);
                return node;
            }
        }
        public abstract class PropertyTargetingNodeData : NodeData {
            
            public string propertyHint;
        }
        public class VariableNodeData : PropertyTargetingNodeData {
            public string path;
            public override GraphElement CreateNode(ActionGraphView graphView, ActionGraphGlobalSettings settings, Rect layout, string guid) {
                var action = graphView.model.actionInfo[actionType.guid];
                var variable = action.variables[path];
                var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, ActionGraphView.CreateFieldPortType(action.type, variable.type.Value));
                outputPort.portName = string.Empty;
                outputPort.AddToClassList(ActionGraphView.OutputPortClassName);
                outputPort.RegisterCallback<PortUpdateEvent>(evt =>
                {
                    var self = (Port)evt.currentTarget;
                    var graphView = self.GetFirstAncestorOfType<ActionGraphView>();

                    if (graphView == null)
                        return;
                    var other = evt.portA == self ? evt.portB : evt.portA;
                    switch (evt.type) {
                        case PortUpdateEventType.Connected:
                            graphView.model[self.node.viewDataKey].next = other.node.viewDataKey;
                            graphView.model.GetData<VariableNodeData>(self.node.viewDataKey).propertyHint = other.viewDataKey;
                            break;
                        case PortUpdateEventType.Disconnected:
                            if (graphView.model[self.node.viewDataKey].next == other.viewDataKey) {
                                graphView.model[self.node.viewDataKey].next = null;
                                graphView.model.GetData<VariableNodeData>(self.node.viewDataKey).propertyHint = null;
                            }
                            break;
                    }
                });
                outputPort.portColor = variable.type.Value.GetColor(Color.white);
                var node = new TokenNode(null, outputPort) { viewDataKey = guid };
                node.title = path;
                node.icon = graphView.VariableIcon;
                node.Q<Image>("icon").tintColor = variable.type.Value.GetColor(Color.white);
                node.SetPosition(layout);
                return node;
            }

            public override ActionModel.Node ToModelNode(Rect layout) => new ActionModel.Node<VariableNodeData>(this, layout);
        }
        public class FieldNodeData : NodeData {
            public string propertyHint;
            public Dictionary<string, object> properties = new Dictionary<string, object>();

            public override GraphElement CreateNode(ActionGraphView graphView, ActionGraphGlobalSettings settings, Rect layout, string guid) {
                throw new NotImplementedException();
            }

            public override ActionModel.Node ToModelNode(Rect layout) => new ActionModel.Node<FieldNodeData>(this, layout);
        }
        public struct VariableInfo {
            public string action;
            public string path;
            public SerializableType type;
        }
        public struct ActionInfo {
            public SerializableType type;
            public Dictionary<string, VariableInfo> variables;
        }
    }
    [Serializable]
    public struct ActionModule {
        public ActionAsset asset;
        public ActionType action;
        public string guid;

        public ActionModule(ActionAsset asset) {
            this.asset = asset;
            this.action = asset.action;
            this.guid = Guid.NewGuid().ToString("N");
        }
    }

    public class ActionModuleSerializer : JsonConverter<ActionModule> {

        public override ActionModule ReadJson(JsonReader reader, Type objectType, ActionModule existingValue, bool hasExistingValue, JsonSerializer serializer) {
            if (reader.TokenType == JsonToken.StartObject) {
                var obj = JObject.Load(reader);
                var target = hasExistingValue ? existingValue : new ActionModule();
                target.asset = AssetDatabase.LoadAssetAtPath<ActionAsset>(AssetDatabase.GUIDToAssetPath(obj[nameof(ActionModule.asset)].Value<string>()));
                target.guid = obj[nameof(ActionModule.guid)].Value<string>();
                return target;
            }
            else {
                throw new JsonException();
            }
        }

        public override void WriteJson(JsonWriter writer, ActionModule value, JsonSerializer serializer) {
            var assetPath = AssetDatabase.GetAssetPath(value.asset);
            writer.WriteStartObject();
            writer.WritePropertyName(nameof(ActionModule.asset));
            writer.WriteValue(string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.GUIDFromAssetPath(assetPath).ToString());
            writer.WritePropertyName(nameof(ActionModule.guid));
            writer.WriteValue(value.guid);
            writer.WriteEndObject();
        }
    }
}