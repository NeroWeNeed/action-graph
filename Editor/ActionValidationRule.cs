using System;
using System.Collections.Generic;
using System.Linq;
using NeroWeNeed.ActionGraph.Editor.Graph;
using NeroWeNeed.Commons;
using UnityEditor.Experimental.GraphView;

[assembly: SearchableAssembly]
namespace NeroWeNeed.ActionGraph.Editor {
    public abstract class ActionValidationRule {
        public abstract bool IsValid(ActionGraphView graphView, NodeCollection nodes);
    }
    public class SingleRootActionValidationRule : ActionValidationRule {
        public override bool IsValid(ActionGraphView graphView, NodeCollection nodes) {
            var roots = nodes.Where(node => node.root).ToList();
            if (roots.Count <= 1) {
                return true;
            }
            else {
                foreach (var root in roots) {
                    graphView.GetNodeByGuid(root.guid).AddError("Graph must have 1 root node.");
                }
                return false;
            }

        }
    }
}