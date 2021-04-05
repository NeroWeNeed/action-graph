using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NeroWeNeed.Commons.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public static class ActionNodeUtility {
        private static VisualElement CreateContainer(BindableElement element, Direction direction, Type type, Type portType, string path) {
            var container = new VisualElement();
            container.Add(element);
            container.AddToClassList("action-graph-field-container");
            var port = Port.Create<Edge>(Orientation.Horizontal, direction, Port.Capacity.Single, portType);
            port.portName = string.Empty;
            port.portColor = type.GetColor(Color.white);
            port.viewDataKey = path;
            port.capabilities ^= Capabilities.Selectable;
            port.tooltip = type.FullName;
            port.AddToClassList(ActionGraphView.SilentPortClassName);
            if (direction == Direction.Output) {
                container.Add(port);
            }
            else {
                container.Insert(0, port);
            }
            container.AddToClassList(ActionGraphView.ElementContainerClassName);
            container.AddToClassList(ActionGraphView.FieldClassName);
            container.viewDataKey = path;
            return container;
        }
        private static Port CreatePort(Direction direction, Type type, FieldInfo fieldInfo, string path, Type portType) {
            var nodePort = Port.Create<Edge>(Orientation.Horizontal, direction, Port.Capacity.Single, portType);
            nodePort.portName = path;
            nodePort.viewDataKey = path;
            nodePort.tooltip = type.FullName;
            nodePort.AddToClassList(ActionGraphView.FieldClassName);
            nodePort.AddToClassList(ActionGraphView.CollectablePortClassName);
            nodePort.AddToClassList(ActionGraphView.FieldPortClassName);
            nodePort.portColor = type.GetColor(Color.white);
            return nodePort;
        }
        public static void RemapGuid<TNodeData>(TNodeData self, Dictionary<string, string> guidMapping) where TNodeData : NodeData, INodeOutput {
            if (guidMapping.TryGetValue(self.Next, out string newGuid)) {
                self.Next = newGuid;
            }
            else {
                self.Next = null;
            }
        }
        /*         public static void RemapGuids<TNodeData>(TNodeData self, Dictionary<string, string> guidMapping) where TNodeData : NodeData, INodeMultiOutput {
                    var newNext = self.Next.Select(next =>
                    {
                        if (guidMapping.TryGetValue(next, out string newGuid)) {
                            return newGuid;
                        }
                        else {
                            return null;
                        }
                    }).Where(next => next != null).ToList();
                    self.Next.Clear();
                    self.Next.AddRange(newNext);
                } */
        /*         private static void OnPortUpdateWithoutContainer(PortUpdateEvent evt) {
                    var self = (Port)evt.target;
                    var selfNode = self.node;
                    var graphView = self.GetFirstAncestorOfType<ActionGraphView>();
                    if (graphView == null || selfNode == null)
                        return;
                    var enabled = evt.type == PortUpdateEventType.Disconnected;
                    selfNode.Query<VisualElement>(null, FieldClassName).Where(e => e.viewDataKey != self.viewDataKey && e.viewDataKey?.StartsWith(self.viewDataKey) == true).ForEach(e => e.SetEnabled(enabled));
                } */

        private static void OnDetachFromPanel(DetachFromPanelEvent evt) {
            var self = (VisualElement)evt.target;
            if (self.userData is Attacher attacher) {
                attacher.Detach();
            }
        }
        private static void OnPortUpdateContainer(PortUpdateEvent evt) {
            var self = (Port)evt.target;
            var selfNode = self.node;
            var graphView = self.GetFirstAncestorOfType<ActionGraphView>();
            var container = selfNode.Query<VisualElement>(null, ActionGraphView.ElementContainerClassName).Where(c => c.viewDataKey == self.viewDataKey).First();
            if (graphView == null || container == null)
                return;
            Port silentOther = selfNode.Query<Port>(null, ActionGraphView.SilentPortClassName).Where(p => p.viewDataKey == self.viewDataKey).First();
            Port other = evt.portA == self ? evt.portB : evt.portA;
            switch (evt.type) {
                case PortUpdateEventType.Connected:
                    if (!other.ClassListContains(ActionGraphView.SilentPortClassName)) {
                        container.visible = false;
                        foreach (var edge in self.connections.Where(e => e.ClassListContains(ActionGraphView.SilentEdgeClassName)).ToList()) {
                            self.Disconnect(edge);
                            graphView.RemoveElement(edge);
                        }
                    }
                    else {
                        container.visible = true;
                    }
                    break;
                case PortUpdateEventType.Disconnected:
                    if (silentOther != null) {
                        var edge = self.ConnectTo(silentOther);
                        edge.capabilities ^= Capabilities.Selectable;
                        edge.AddToClassList(ActionGraphView.SilentEdgeClassName);
                        graphView.AddElement(edge);
                        container.visible = true;
                    }
                    break;
                default:
                    break;
            }
        }
        private static void HandleFieldLayout(Node target, Direction direction, VisualElement nodeContainer, Type type, Type portType, FieldInfo fieldInfo, BindableElement element, string path) {
            var port = CreatePort(direction, type, fieldInfo, path, portType);
            nodeContainer.Add(port);
            if (element != null) {
                var container = CreateContainer(element, direction == Direction.Input ? Direction.Output : Direction.Input, type, portType, path);
                port.RegisterCallback<PortUpdateEvent>(OnPortUpdateContainer);
                element.style.minWidth = 50;
                target.Add(container);
                Attacher attacher = new Attacher(container, port, direction == Direction.Input ? SpriteAlignment.LeftCenter : SpriteAlignment.RightCenter);
                attacher.Reattach();
                port.userData = attacher;
                container.userData = attacher;
                port.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
                container.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            }
        }
        public static void CreateNodeField(Node target, Type portType, Type fieldType, FieldInfo fieldInfo, BindableElement element, string path) {
            var layout = NodeLayout.Input;
            var typeAttr = fieldType.GetCustomAttribute<NodeLayoutAttribute>();
            if (typeAttr != null)
                layout = typeAttr.value;
            var fieldAttr = fieldInfo.GetCustomAttribute<NodeLayoutAttribute>();
            if (fieldAttr != null)
                layout = fieldAttr.value;
            
            switch (layout) {
                case NodeLayout.Input:
                    HandleFieldLayout(target, Direction.Input, target.inputContainer, fieldType, portType, fieldInfo, element, path);
                    break;
                case NodeLayout.Output:
                    HandleFieldLayout(target, Direction.Output, target.outputContainer, fieldType, portType, fieldInfo, element, path);
                    break;
            }
        }
    }
}