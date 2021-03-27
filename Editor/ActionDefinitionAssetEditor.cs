using System.Linq;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor {
    [CustomEditor(typeof(ActionDefinitionAsset))]
    public class ActionDefinitionAssetEditor : UnityEditor.Editor {
        public const string Uxml = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionDefinitionAssetEditor.uxml";
        protected VisualElement rootElement;
        public override VisualElement CreateInspectorGUI() {
            rootElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree();
            var self = (ActionDefinitionAsset)serializedObject.targetObject;
            if (self.delegateType.IsCreated) {
                var returnType = self.delegateType.Value.GetMethod("Invoke").ReturnType;
                if (ProjectUtility.GetOrCreateProjectAsset<ActionReturnTypeAggregatorSchema>().data.TryGetValue(returnType, out var aggregators)) {
                    var field = new PopupField<SerializableMethod>("Aggregator", aggregators, 0, (method) => $"{method.container.FullName}::{method.name}", (method) => $"{method.container.FullName}/{method.name}");
                    if (self.aggregator.IsCreated) {
                        field.value = self.aggregator;
                    }
                    else {
                        self.aggregator = aggregators.First();
                    }
                    field.RegisterValueChangedCallback(evt =>
                    {
                        ((ActionDefinitionAsset)serializedObject.targetObject).aggregator = evt.newValue;
                        EditorUtility.SetDirty(serializedObject.targetObject);
                    });
                    rootElement.Q<VisualElement>("aggregator-container").Add(field);
                }

            }

            return rootElement;
        }

    }
}