using System;
using System.Linq;
using System.Reflection;
using NeroWeNeed.Commons.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public class NodeTypeFieldProviderContext : ITypeFieldProviderContext {
        public const string ContainerClass = "action-graph-field-container";
        private Node target;
        private Type action;

        public NodeTypeFieldProviderContext(Node target, Type action) {
            this.target = target;
            this.action = action;
        }
        private void HandleFieldLayout(Direction direction, VisualElement nodeContainer, Type type, FieldInfo fieldInfo, BindableElement element, VisualElement elementContainer, SpriteAlignment alignment) {
            var nodePort = Port.Create<Edge>(Orientation.Horizontal, direction, Port.Capacity.Single, ActionGraphView.CreateFieldPortType(action, type));
            var elementContainerPort = Port.Create<Edge>(Orientation.Horizontal, direction == Direction.Input ? Direction.Output : Direction.Input, Port.Capacity.Single, ActionGraphView.CreateFieldPortType(action, type));

            elementContainerPort.portName = string.Empty;
            elementContainerPort.portColor = type.GetColor(Color.white);
            elementContainerPort.viewDataKey = element.bindingPath;
            elementContainerPort.capabilities ^= Capabilities.Selectable;
            
            elementContainerPort.AddToClassList(ActionGraphView.SilentPortClassName);
            nodePort.portName = element.bindingPath;
            nodePort.viewDataKey = element.bindingPath;

            nodePort.AddToClassList(ActionGraphView.CollectablePortClassName);
            nodePort.AddToClassList(ActionGraphView.FieldPortClassName);
            nodePort.portColor = type.GetColor(Color.white);
            nodeContainer.Add(nodePort);
            if (nodePort.direction == Direction.Input) {
                elementContainer.Add(elementContainerPort);
            }
            else {
                elementContainer.Insert(0, elementContainerPort);
            }
            elementContainer.AddToClassList(ActionGraphView.ElementContainerClassName);
            elementContainer.viewDataKey = element.bindingPath;
            nodePort.RegisterCallback<PortUpdateEvent>(evt =>
            {
                var self = (Port)evt.target;
                var selfNode = self.node;
                var graphView = self.GetFirstAncestorOfType<ActionGraphView>();
                
                if (graphView == null)
                    return;
                Port silentOther = selfNode.Query<Port>(null, ActionGraphView.SilentPortClassName).Where(p => p.viewDataKey == self.viewDataKey).First();
                Port other = evt.portA == self ? evt.portB : evt.portA;
                switch (evt.type) {
                    case PortUpdateEventType.Connected:
                        if (!other.ClassListContains(ActionGraphView.SilentPortClassName)) {
                            elementContainer.visible = false;
                            foreach (var edge in self.connections.Where(e => e.ClassListContains(ActionGraphView.SilentEdgeClassName)).ToList())
                            {
                                self.Disconnect(edge);
                                graphView.RemoveElement(edge);
                            }
                        }
                        else {
                            
                            elementContainer.visible = true;
                        }
                        break;
                    case PortUpdateEventType.Disconnected:

                        if (silentOther != null) {
                            var edge = self.ConnectTo(silentOther);
                            edge.capabilities ^= Capabilities.Selectable;
                            edge.AddToClassList(ActionGraphView.SilentEdgeClassName);
                            graphView.AddElement(edge);
                            elementContainer.visible = true;
                        }

                        break;
                    default:
                        break;
                }
            });
            element.style.minWidth = 94;

            target.Add(elementContainer);
            Attacher attacher = new Attacher(elementContainer, nodePort, alignment);
            attacher.Reattach();
        }

        public void HandleField(Type type, FieldInfo fieldInfo, BindableElement element) {
            var container = new VisualElement();
            container.AddToClassList("action-graph-field-container");
            container.Add(element);

            var layout = NodeLayout.Input;
            var typeAttr = type.GetCustomAttribute<NodeLayoutAttribute>();
            if (typeAttr != null)
                layout = typeAttr.value;
            var fieldAttr = fieldInfo.GetCustomAttribute<NodeLayoutAttribute>();
            if (fieldAttr != null)
                layout = fieldAttr.value;
            switch (layout) {
                case NodeLayout.Input:
                    HandleFieldLayout(Direction.Input, target.inputContainer, type, fieldInfo, element, container, SpriteAlignment.LeftCenter);
                    break;
                case NodeLayout.Output:
                    HandleFieldLayout(Direction.Output, target.outputContainer, type, fieldInfo, element, container, SpriteAlignment.RightCenter);
                    break;
                case NodeLayout.Extension:
                    target.extensionContainer.Add(container);
                    break;
                default:
                    break;
            }



        }

    }
}