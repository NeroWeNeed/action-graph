using System.Collections.Generic;
using NeroWeNeed.ActionGraph.Editor.Graph;
using NeroWeNeed.Commons.Editor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor {
    public class Inspector : GraphElement, ISelection {
        public const string Uxml = "Packages/github.neroweneed.action-graph/Editor/Resources/Inspector.uxml";
        private ActionGraphView graphView;
        private VisualElement rootElement;
        private VisualElement variableContainer;
        private VisualElement propertyContainer;
        public List<ISelectable> selection => graphView?.selection;

        public Inspector(ActionGraphView graphView) {
            this.graphView = graphView;
            rootElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree();
            this.style.paddingBottom = this.style.paddingLeft = this.style.paddingRight = this.style.paddingTop = 0;
            variableContainer = rootElement.Q<VisualElement>("variables");
            propertyContainer = rootElement.Q<VisualElement>("properties");
            this.Add(rootElement);
            this.capabilities = Capabilities.Collapsible | Capabilities.Movable | Capabilities.Resizable;
            this.SetPosition(new Rect(0, 0, 240, 320));
            this.AddManipulator(new Dragger
            {
                clampToParentEdges = true
            });
            var resizer = new Resizer();
            this.Add(resizer);
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
        public void RefreshProperties() {
            this.propertyContainer.Clear();
            this.propertyContainer.Unbind();
            var obj = graphView.model.context.Container;
            if (obj == null)
                return;
            var serializedObject = new SerializedObject(obj);
            foreach (var path in obj.GetType().GetPropertyPaths(fieldInfo => !(typeof(ActionAsset).IsAssignableFrom(fieldInfo.FieldType)))) {
                propertyContainer.Add(new PropertyField(serializedObject.FindProperty(path)));
            }
            propertyContainer.Bind(serializedObject);
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