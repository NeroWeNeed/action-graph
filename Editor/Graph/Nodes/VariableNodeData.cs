using System.Collections.Generic;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public class VariableNodeData : NodeData, IPropertyConnector, INodeOutput, IActionElement {
        public string path;
        public string propertyHint;
        public string next;
        public ActionId actionId;
        [JsonIgnore]
        public string PropertyHint { get => propertyHint; set => propertyHint = value; }
        [JsonIgnore]
        public string Next { get => next; set => next = value; }

        public ActionId ActionId { get => actionId; }

        public override GraphElement CreateNode(ActionGraphView graphView, ActionGraphGlobalSettings settings, Rect layout, string guid) {
            var action = graphView.model.actionInfo[actionId.guid];
            var variable = action.variables[path];
            var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, ActionGraphView.CreateFieldPortType(action.type, variable.type.Value));
            outputPort.portName = string.Empty;
            outputPort.AddToClassList(ActionGraphView.OutputPortClassName);
            outputPort.AddToClassList(ActionGraphView.NodeConnectionPortClassName);
            outputPort.RegisterCallback<PortUpdateEvent>(evt =>
            {
                var self = (Port)evt.currentTarget;
                var graphView = self.GetFirstAncestorOfType<ActionGraphView>();

                if (graphView == null)
                    return;
                var other = evt.portA == self ? evt.portB : evt.portA;
                var data = graphView.model.GetData<VariableNodeData>(self.node.viewDataKey);
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
            outputPort.portColor = variable.type.Value.GetColor(Color.white);
            var node = new TokenNode(null, outputPort) { viewDataKey = guid };
            node.AddToClassList(ActionGraphView.VariableNodeClassName);
            
            node.title = path;
            node.icon = graphView.VariableIcon;
            node.Q<Image>("icon").tintColor = variable.type.Value.GetColor(Color.white);
            node.SetPosition(layout);
            return node;
        }

        public override void RemapGuid(Dictionary<string, string> guidMapping) {
            ActionNodeUtility.RemapGuid(this, guidMapping);
        }

        public override ActionAssetModel.Node ToModelNode(Rect layout) => new ActionAssetModel.Node<VariableNodeData>(this, layout);
    }
}