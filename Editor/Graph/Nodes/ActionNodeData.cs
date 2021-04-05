using System;
using System.Collections.Generic;
using System.Reflection;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public class ActionNodeData : NodeData, IPropertyContainer, INodeOutput, IActionElement {
        public string identifier;
        public string subIdentifier;
        public string next;
        public ActionId actionId;
        [JsonProperty]
        private Dictionary<string, object> properties = new Dictionary<string, object>();
        [JsonIgnore]
        public Dictionary<string, object> Properties { get => properties; }
        [JsonIgnore]
        public string Next { get => next; set => next = value; }
        public ActionId ActionId { get => actionId; }

        public override ActionAssetModel.Node ToModelNode(Rect layout) => new ActionAssetModel.Node<ActionNodeData>(this, layout);
        public override GraphElement CreateNode(ActionGraphView graphView, ActionGraphGlobalSettings settings, Rect layout, string guid = null) {
            var info = settings[ActionId];
            var actionSchema = ProjectUtility.GetOrCreateProjectAsset<ActionSchema>();
            //var nodeInfo = settings.GetAction(actionId, identifier);
            var nodeInfo = actionSchema.data[info.delegateType][identifier];
            if (string.IsNullOrEmpty(guid))
                guid = Guid.NewGuid().ToString("N");
            var node = new Node()
            {
                viewDataKey = guid,
                title = nodeInfo.Name
            };
            node.AddToClassList(ActionGraphView.ActionNodeClassName);
            node.capabilities ^= Capabilities.Collapsible;
            var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, ActionGraphView.CreateActionPortType(info.delegateType));
            inputPort.portName = "in";
            inputPort.AddToClassList(ActionGraphView.NodePortClassName);
            inputPort.AddToClassList(ActionGraphView.NodeInputPortClassName);
            inputPort.AddToClassList(ActionGraphView.CollectablePortClassName);
            inputPort.portColor = info.delegateType.Value.GetColor(Color.white);
            node.inputContainer.Add(inputPort);
            var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, ActionGraphView.CreateActionPortType(info.delegateType));
            outputPort.portName = "out";

            outputPort.portColor = info.delegateType.Value.GetColor(Color.white);
            outputPort.AddToClassList(ActionGraphView.NodePortClassName);
            outputPort.AddToClassList(ActionGraphView.NodeOutputPortClassName);
            outputPort.AddToClassList(ActionGraphView.OutputPortClassName);
            outputPort.AddToClassList(ActionGraphView.NodeConnectionPortClassName);
            outputPort.RegisterCallback<PortUpdateEvent>(evt =>
            {
                var self = (Port)evt.currentTarget;
                var graphView = self.GetFirstAncestorOfType<ActionGraphView>();

                if (graphView == null)
                    return;
                var data = graphView.model.GetData<ActionNodeData>(self.node.viewDataKey);
                var other = evt.portA == self ? evt.portB : evt.portA;
                switch (evt.type) {
                    case PortUpdateEventType.Connected:
                        data.next = other.node.viewDataKey;
                        break;
                    case PortUpdateEventType.Disconnected:
                        if (data.next == other.viewDataKey)
                            data.next = null;
                        break;
                }
            });
            node.outputContainer.Add(outputPort);

            if (nodeInfo.VariantCount > 1) {
                node.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
                {
                    var self = (Node)evt.target;
                    var settings = ProjectUtility.GetProjectSettings<ActionGraphGlobalSettings>();
                    var graphView = self.GetFirstAncestorOfType<ActionGraphView>();
                    if (settings == null || graphView == null)
                        return;
                    var data = graphView.model.GetData<ActionNodeData>(self.viewDataKey);
                    var action = settings.GetAction(data.ActionId, data.identifier);

                    foreach (var method in action.variants) {
                        evt.menu.AppendAction($"Type/{method.Key}", (a) =>
                        {
                            data.subIdentifier = (string)a.userData;
                            data.BuildNodeContents(graphView, self, settings, settings[data.ActionId], action, true);
                            graphView.RefreshNodeConnections();
                        }, (a) => data.subIdentifier == (string)a.userData ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal, method.Key);
                    }
                    evt.menu.AppendSeparator();
                });

                //node.titleContainer.Add(methodSelector);
            }
            BuildNodeContents(graphView, node, settings, info, nodeInfo);
            node.RegisterCallback<FieldUpdateEvent>(evt =>
            {
                var element = (Node)evt.currentTarget;
                var graphView = element.GetFirstAncestorOfType<ActionGraphView>();
                if (graphView == null)
                    return;
                if (evt.value == default) {
                    graphView.model.GetData<ActionNodeData>(element.viewDataKey).properties.Remove(evt.path);
                }
                else {
                    graphView.model.GetData<ActionNodeData>(element.viewDataKey).properties[evt.path] = evt.value;
                }

            });
            node.RefreshExpandedState();
            node.SetPosition(layout);

            return node;
        }
        private void BuildNodeContents(ActionGraphView graphView, Node node, ActionGraphGlobalSettings settings, ActionDefinitionAsset info, ActionSchema.Action action, bool clean = false) {
            if (clean) {
                foreach (var item in node.Query<VisualElement>(null, ActionGraphView.FieldClassName).ToList()) {
                    if (item is Port port) {
                        if (port.connected) {
                            foreach (var edge in port.connections) {
                                graphView.RemoveElement(edge);
                            }
                        }
                        graphView.RemoveElement(port);
                    }
                    else if (item is GraphElement graphElement) {
                        graphView.RemoveElement(graphElement);
                    }
                    else {
                        item.RemoveFromHierarchy();
                    }
                }
            }
            var schema = ProjectUtility.GetOrCreateProjectAsset<TypeFieldSchema>();
            var method = string.IsNullOrEmpty(subIdentifier) ? action.GetDefaultVariant() : action.variants[subIdentifier];
            if (method.config.IsCreated) {
                method.config.Value.Decompose((Type type, FieldInfo fieldInfo, FieldInfo parentFieldInfo, string path, string parentPath, TypeDecompositionOptions _) =>
                {
                    object initialValue;
                    type = type.GetCustomAttribute<GraphInstanceTypeAttribute>()?.type ?? type;
                    if (properties.TryGetValue(path, out object value)) {
                        if (value is IConvertible) {
                            try {
                                initialValue = Convert.ChangeType(value, type);
                            }
                            catch (Exception) {
                                initialValue = Activator.CreateInstance(type);
                                properties[path] = initialValue;
                            }
                        }
                        else {
                            initialValue = value;
                        }
                    }
                    else {
                        initialValue = Activator.CreateInstance(type);
                    }
                    bool terminal;
                    if (ProjectUtility.GetOrCreateProjectAsset<NodeLayoutHandlerSchema>().data.TryGetValue(type, out var handler)) {
                        terminal = handler.Value.HandleLayout(type, fieldInfo, ActionGraphView.CreateActionPortType(info.delegateType), path, node, initialValue);
                    }
                    else {
                        terminal = schema.CreateField(type, fieldInfo, initialValue, out BindableElement element);
                        ActionNodeUtility.CreateNodeField(node, ActionGraphView.CreateFieldPortType(info.delegateType, type), type, fieldInfo, element, path);
                        if (terminal) {
                            element.bindingPath = path;
                        }
                    }


                    return terminal;
                }, new TypeDecompositionOptions
                {
                    exploreChildren = true
                });
            }
        }

        public override void RemapGuid(Dictionary<string, string> guidMapping) {
            ActionNodeUtility.RemapGuid(this, guidMapping);
        }
    }
}