using System;
using System.Reflection;
using NeroWeNeed.ActionGraph.Editor.Graph;
using NeroWeNeed.Commons.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph {
    [NodeLayoutHandler(typeof(NodeReference))]
    public class NodeReferenceLayoutHandler : NodeLayoutHandler<NodeReference> {
        public override bool HandleLayout(Type type, FieldInfo fieldInfo, Type actionType, string path, Node node, NodeReference value) {
            var port = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, ActionGraphView.CreateActionPortType(actionType));
            port.portName = path;
            port.viewDataKey = path;
            port.tooltip = type.FullName;
            port.AddToClassList(ActionGraphView.FieldClassName);
            port.AddToClassList(ActionGraphView.CollectablePortClassName);
            port.AddToClassList(ActionGraphView.FieldPortClassName);
            port.AddToClassList(ActionGraphView.NodeConnectionPortClassName);
            port.portColor = actionType.GetColor(Color.white);
            node.outputContainer.Add(port);
            port.RegisterCallback<PortUpdateEvent>(evt =>
            {
                var self = (Port)evt.currentTarget;
                var graphView = self.GetFirstAncestorOfType<ActionGraphView>();
                if (graphView == null)
                    return;
                var other = evt.portA == self ? evt.portB : evt.portA;
                var data = graphView.model[self.node.viewDataKey];
                if (data is IPropertyContainer container) {

                    switch (evt.type) {
                        case PortUpdateEventType.Connected:
                            container.Properties[self.viewDataKey] = new NodeReference(other.node.viewDataKey);
                            break;
                        case PortUpdateEventType.Disconnected:
                            if (container.Properties.TryGetValue(self.node.viewDataKey, out object value) && value is INodeReference nodeReference && nodeReference.Get() == other.viewDataKey) {
                                nodeReference.Set(null);
                                container.Properties[self.viewDataKey] = nodeReference;
                            }
                            break;
                    }


                }
            });
            return true;
        }
    }
}