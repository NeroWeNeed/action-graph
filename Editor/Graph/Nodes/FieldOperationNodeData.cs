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
    public class FieldOperationNodeData : NodeData, IPropertyContainer, IPropertyConnector, INodeOutput {
        public string identifier;
        public string subIdentifier;
        public string propertyHint;
        public string next;
        [JsonIgnore]
        public string PropertyHint { get => propertyHint; set => propertyHint = value; }
        [JsonProperty]
        private Dictionary<string, object> properties = new Dictionary<string, object>();
        [JsonIgnore]
        public Dictionary<string, object> Properties { get => properties; }
        [JsonIgnore]
        public string Next { get => next; set => next = value; }

        public override ActionAssetModel.Node ToModelNode(Rect layout) => new ActionAssetModel.Node<FieldOperationNodeData>(this, layout);
        public override GraphElement CreateNode(ActionGraphView graphView, ActionGraphGlobalSettings settings, Rect layout, string guid = null) {

            var fieldOperationSchema = ProjectUtility.GetOrCreateProjectAsset<FieldOperationSchema>();
            //var nodeInfo = settings.GetAction(actionId, identifier);
            var nodeInfo = fieldOperationSchema.data[identifier];
            FieldOperationSchema.Operation.Variant callVariantInfo;
            if (string.IsNullOrEmpty(subIdentifier) || !nodeInfo.callVariants.TryGetValue(subIdentifier, out callVariantInfo)) {
                callVariantInfo = nodeInfo.GetDefaultVariant();
            }

            if (string.IsNullOrEmpty(guid))
                guid = Guid.NewGuid().ToString("N");
            var node = new Node()
            {
                viewDataKey = guid,
                title = callVariantInfo.displayName
            };
            node.AddToClassList(ActionGraphView.FieldOperationNodeClassName);
            node.capabilities ^= Capabilities.Collapsible;

            if (nodeInfo.CallVariantCount > 1) {
                node.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
                {
                    var self = (Node)evt.target;
                    var operations = ProjectUtility.GetProjectAsset<FieldOperationSchema>();
                    var graphView = self.GetFirstAncestorOfType<ActionGraphView>();
                    if (operations == null || graphView == null)
                        return;
                    var data = graphView.model.GetData<FieldOperationNodeData>(self.viewDataKey);
                    if (operations.data.TryGetValue(data.identifier, out FieldOperationSchema.Operation operation)) {
                        foreach (var callVariant in operation.callVariants) {
                            evt.menu.AppendAction($"Type/{callVariant.Key}", (a) =>
                            {
                                data.subIdentifier = (string)a.userData;
                                data.BuildNodeContents(graphView, self, settings, nodeInfo, callVariant.Value, true);
                                graphView.RefreshNodeConnections();
                            }, (a) => data.subIdentifier == (string)a.userData ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal, callVariant.Key);
                        }
                        evt.menu.AppendSeparator();
                    }
                });

                //node.titleContainer.Add(methodSelector);
            }
            BuildNodeContents(graphView, node, settings, nodeInfo, callVariantInfo);

            node.RegisterCallback<FieldUpdateEvent>(evt =>
            {
                var element = (Node)evt.currentTarget;
                var graphView = element.GetFirstAncestorOfType<ActionGraphView>();
                if (graphView == null)
                    return;
                if (evt.value == default) {
                    graphView.model.GetData<FieldOperationNodeData>(element.viewDataKey).properties.Remove(evt.path);
                }
                else {
                    graphView.model.GetData<FieldOperationNodeData>(element.viewDataKey).properties[evt.path] = evt.value;
                }

            });
            node.RefreshExpandedState();
            node.SetPosition(layout);

            return node;
        }
        private void BuildNodeContents(ActionGraphView graphView, Node node, ActionGraphGlobalSettings settings, FieldOperationSchema.Operation operation, FieldOperationSchema.Operation.Variant callVariant, bool clean = false) {
            if (clean) {
                var elements = new List<VisualElement>();
                elements.AddRange(node.Query<VisualElement>(null, ActionGraphView.FieldClassName).ToList());
                elements.AddRange(node.Query<VisualElement>(null, ActionGraphView.FieldOperationOutputPortClassName).ToList());
                foreach (var item in elements) {
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
            var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, ActionGraphView.CreateFieldOperationPortType(callVariant.outputType));
            outputPort.portName = "out";
            outputPort.portColor = callVariant.outputType.Value.GetColor(Color.white);
            outputPort.AddToClassList(ActionGraphView.NodePortClassName);
            outputPort.AddToClassList(ActionGraphView.NodeOutputPortClassName);
            outputPort.AddToClassList(ActionGraphView.OutputPortClassName);
            outputPort.AddToClassList(ActionGraphView.NodeConnectionPortClassName);
            outputPort.AddToClassList(ActionGraphView.FieldOperationOutputPortClassName);
            outputPort.RegisterCallback<PortUpdateEvent>(evt =>
            {
                var self = (Port)evt.currentTarget;
                var graphView = self.GetFirstAncestorOfType<ActionGraphView>();

                if (graphView == null)
                    return;
                var data = graphView.model.GetData<FieldOperationNodeData>(self.node.viewDataKey);
                var other = evt.portA == self ? evt.portB : evt.portA;
                switch (evt.type) {
                    case PortUpdateEventType.Connected:
                        data.next = other.node.viewDataKey;
                        data.propertyHint = other.viewDataKey;
                        break;
                    case PortUpdateEventType.Disconnected:
                        if (data.next == other.viewDataKey) {
                            data.next = null;
                            data.propertyHint = null;
                        }
                        break;
                }
            });
            node.outputContainer.Add(outputPort);
            if (callVariant.configType.IsCreated) {
                callVariant.configType.Value.Decompose((Type type, FieldInfo fieldInfo, FieldInfo parentFieldInfo, string path, string parentPath, TypeDecompositionOptions _) =>
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
                        terminal = handler.Value.HandleLayout(type, fieldInfo, ActionGraphView.CreateFieldOperationPortType(type), path, node, initialValue);
                    }
                    else {
                        terminal = schema.CreateField(type, fieldInfo, initialValue, out BindableElement element);
                        ActionNodeUtility.CreateNodeField(node, ActionGraphView.CreateFieldOperationPortType(type), type, fieldInfo, element, path);
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