using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public class FieldTransformerNodeData : NodeData {
        public string propertyHint;
        public Dictionary<string, object> properties = new Dictionary<string, object>();

        public override GraphElement CreateNode(ActionGraphView graphView, ActionGraphGlobalSettings settings, Rect layout, string guid) {
            throw new NotImplementedException();
        }

        public override ActionAssetModel.Node ToModelNode(Rect layout) => new ActionAssetModel.Node<FieldTransformerNodeData>(this, layout);
    }
}