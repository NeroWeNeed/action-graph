using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NeroWeNeed.Commons.Editor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public partial class ActionGraphView : GraphView {
        public const string NodePortClassName = "action-graph-node-port";
        public const string CollectablePortClassName = "action-graph-collectable-port";
        public const string NodeInputPortClassName = "action-graph-input-node-port";
        public const string NodeOutputPortClassName = "action-graph-output-node-port";
        public const string OutputPortClassName = "action-graph-output-port";
        public const string MasterNodePortClassName = "action-graph-master-node-port";
        public const string SilentPortClassName = "action-graph-silent-port";
        public const string ElementContainerClassName = "action-graph-element-container";
        public const string SilentEdgeClassName = "action-graph-silent-edge";
        public const string VariableIconPath = "Packages/github.neroweneed.action-graph/Editor/Resources/VariableIcon.png";

        private Texture variableIcon;
        public Texture VariableIcon { get => variableIcon ?? AssetDatabase.LoadAssetAtPath<Texture>(VariableIconPath); }

    }

}
