using System;
using System.Runtime.InteropServices;
using NeroWeNeed.Commons.Editor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using static NeroWeNeed.ActionGraph.Editor.Graph.ActionGraphView;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public class MasterNode : Node {
        public MasterNode() {
            this.viewDataKey = Guid.Empty.ToString("N");
            this.title = "Master";
        }

        public void AddModule(ActionModule module) {
            var settings = ProjectUtility.GetProjectSettings<ActionGraphGlobalSettings>();
            var port = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, CreateActionPortType(settings[module.action].delegateType));
            port.viewDataKey = module.guid;
            port.AddToClassList(MasterNodePortClassName);
            port.portName = settings[module.action].Name;
            this.capabilities = Capabilities.Collapsible | Capabilities.Movable | Capabilities.Resizable | Capabilities.Selectable;
            port.RegisterCallback<PortUpdateEvent>(evt =>
            {
                var self = (Port)evt.target;
                var other = evt.portA == self ? evt.portB : evt.portA;

                var graphView = self.GetFirstAncestorOfType<ActionGraphView>();
                if (graphView == null)
                    return;
                var node = graphView.model[other.node.viewDataKey];
                if (node is ActionNodeData actionNode) {
                    switch (evt.type) {
                        case PortUpdateEventType.Connected:
                            actionNode.moduleHint = self.viewDataKey;
                            break;
                        case PortUpdateEventType.Disconnected:
                            actionNode.moduleHint = null;
                            break;
                    }
                }

            });
            this.inputContainer.Add(port);
            this.RefreshExpandedState();
        }
    }
}