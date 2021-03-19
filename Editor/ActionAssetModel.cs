using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NeroWeNeed.ActionGraph.Editor.Graph;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace NeroWeNeed.ActionGraph.Editor {
    [Serializable]
    public class ActionAssetModel {
        public ActionId id;        
        public Rect masterNodeLayout;
        public Dictionary<string, Node> nodes = new Dictionary<string, Node>();

        [Serializable]
        public abstract class Node {
            public Rect layout;
            public abstract NodeData Data { get; }
            public abstract GraphElement CreateNode(ActionGraphView graphView, ActionGraphGlobalSettings settings, Rect layout, string guid = null);

        }
        [Serializable]
        public class Node<TData> : Node where TData : NodeData {
            public TData data;
            [JsonIgnore]
            public override NodeData Data { get => data; }
            public Node() {
            }

            public Node(TData data, Rect layout) {
                this.data = data;
                this.layout = layout;
            }
            public override GraphElement CreateNode(ActionGraphView graphView, ActionGraphGlobalSettings settings, Rect layout, string guid = null) => data.CreateNode(graphView, settings, layout, guid);

        }

    }
}