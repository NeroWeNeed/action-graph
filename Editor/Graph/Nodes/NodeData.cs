using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public abstract class NodeData {
        
        //public string next;
        public string moduleHint;
        public abstract void RemapGuid(Dictionary<string, string> guidMapping);
        public abstract ActionAssetModel.Node ToModelNode(Rect layout);
        public abstract GraphElement CreateNode(ActionGraphView graphView, ActionGraphGlobalSettings settings, Rect layout, string guid);
    }
}