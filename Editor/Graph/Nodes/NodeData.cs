using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public abstract class NodeData {
        public ActionId actionId;
        public string next;
        public string moduleHint;
        public abstract ActionAssetModel.Node ToModelNode(Rect layout);
        public virtual void RemapGuid(Dictionary<string, string> guidMapping) {
            if (guidMapping.TryGetValue(this.next, out string newGuid)) {
                next = newGuid;
            }
            else {
                next = null;
            }


        }
        public abstract GraphElement CreateNode(ActionGraphView graphView, ActionGraphGlobalSettings settings, Rect layout, string guid);
    }
}