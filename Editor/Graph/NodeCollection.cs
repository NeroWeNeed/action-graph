using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public class NodeCollection : IEnumerable<NodeCollection.NodeInfo> {
        private readonly HashSet<NodeInfo> nodes;
        private static HashSet<NodeInfo> CollectNodes(Port port) {
            var guids = new HashSet<string>();
            var info = new HashSet<NodeInfo>();
            CollectNodes(port, guids, info);
            return info;
        }
        private static void CollectNodes(Port port, HashSet<string> guids, HashSet<NodeInfo> info) {
            foreach (var connection in port.connections) {
                var node = connection.output == port ? connection.input.node : connection.output.node;
                
                if (guids.Add(node.viewDataKey)) {
                    bool root = node.ClassListContains(ActionGraphView.ActionNodeClassName);

                    foreach (var inputPort in node.Query<Port>(null, ActionGraphView.CollectablePortClassName).ToList()) {
                        if (inputPort.ClassListContains(ActionGraphView.NodeInputPortClassName) && root) {
                            root = !inputPort.connected;
                        }
                        CollectNodes(inputPort, guids, info);
                    }
                    info.Add(new NodeInfo { guid = node.viewDataKey, root = root });
                }
            }
        }
        public void Remove(string guid) {
            nodes.RemoveWhere(info => info.guid == guid);
        }
        public void Remove(IEnumerable<string> guids) {
            nodes.RemoveWhere(info => guids.Contains(info.guid));
        }

        public IEnumerator<NodeInfo> GetEnumerator() {
            return nodes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return nodes.GetEnumerator();
        }

        public NodeCollection(Port startPort) {
            nodes = CollectNodes(startPort);
        }
        public struct NodeInfo : IEquatable<NodeInfo> {
            public string guid;
            public bool root;

            public bool Equals(NodeInfo other) {
                return guid == other.guid;
            }
            public override bool Equals(object other) {
                return other is NodeInfo info && guid == info.guid;
            }

            public override int GetHashCode() {
                int hashCode = -770743503;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(guid);
                return hashCode;
            }
        }
    }
}