using System.Collections.Generic;
using NeroWeNeed.ActionGraph.Editor.Graph;
using NeroWeNeed.Commons.Editor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor {
    public class Inspector : GraphElement, ISelection {
        public const string Uxml = "Packages/github.neroweneed.action-graph/Editor/Resources/Inspector.uxml";
        private ActionGraphView graphView;
        private VisualElement rootElement;
        private VisualElement variableContainer;
        public List<ISelectable> selection => graphView?.selection;

        public Inspector(ActionGraphView graphView) {
            this.graphView = graphView;
            rootElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree();
            this.style.paddingBottom = this.style.paddingLeft = this.style.paddingRight = this.style.paddingTop = 0;
            variableContainer = rootElement.Q<VisualElement>("variables");
            this.Add(rootElement);
            //base.hierarchy.Add(rootElement);
            this.capabilities = Capabilities.Collapsible | Capabilities.Movable | Capabilities.Resizable;
            this.SetPosition(new Rect(0, 0, 360, 640));
            this.AddManipulator(new Dragger
            {
                clampToParentEdges = true
            });
            var resizer = new Resizer();
            /*             resizer.style.opacity = 0;
                        base.style.overflow = Overflow.Hidden; */
            this.Add(resizer);
            //base.hierarchy.Add(resizer);
        }
        public void RefreshVariables() {
            this.variableContainer.Clear();
            foreach (var data in this.graphView.model.actionInfo) {
                foreach (var variable in data.Value.variables) {
                    var field = new BlackboardField(graphView.VariableIcon, variable.Value.path, variable.Value.type.Value.FullName)
                    {
                        userData = (data.Key, variable.Value.path)
                    };
                    field.Q<Image>("icon").tintColor = variable.Value.type.Value.GetColor(Color.white);
                    this.variableContainer.Add(field);
                }
            }
        }

        public void AddToSelection(ISelectable selectable) {
            graphView?.AddToSelection(selectable);
        }

        public void RemoveFromSelection(ISelectable selectable) {
            graphView?.RemoveFromSelection(selectable);
        }

        public void ClearSelection() {
            graphView?.ClearSelection();
        }

    }
}