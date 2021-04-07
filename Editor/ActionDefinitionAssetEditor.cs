using System.Linq;
using NeroWeNeed.ActionGraph.Editor.CodeGen;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using Unity.Entities;
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

            rootElement.Query<TextField>(null, "requires-editor-dll-rebuild").ForEach(ele =>
            {
                ele.isDelayed = true;
                ele.RegisterValueChangedCallback(e => {
/*                     AssetDatabase.SaveAssets();
                    ActionEditorUtilityProducer.CreateAssembly(); */
                });
            });
            var delegateType = rootElement.Q<PropertyField>("delegateType");
            delegateType.RegisterCallback<ChangeEvent<SerializableType>>(evt =>
            {
                if (evt.newValue != evt.previousValue) {
                    if (ActionDefinitionAsset.LoadAll().Any(a => a != serializedObject.targetObject && a.delegateType == evt.newValue)) {
                        Debug.LogError("ActionDefinitions must have unique delegates.");
                        ((ActionDefinitionAsset)serializedObject.targetObject).delegateType = evt.previousValue;
                        EditorUtility.SetDirty(serializedObject.targetObject);

                    }
                }
            });
            rootElement.Query<PropertyField>(null, "requires-dll-rebuild--type").ForEach(ele => ele.RegisterCallback<ChangeEvent<SerializableType>>(evt => UpdateAssemblyData()));
            rootElement.Query<PropertyField>(null, "requires-dll-rebuild--method").ForEach(ele => ele.RegisterCallback<ChangeEvent<SerializableMethod>>(evt => UpdateAssemblyData()));
            rootElement.Query<PropertyField>(null, "requires-dll-rebuild--bool").ForEach(ele => ele.RegisterValueChangeCallback(evt => UpdateAssemblyData()));
            var aggregator = rootElement.Q<PropertyField>("aggregator");
            BindAggregatorField(delegateType, aggregator);
            return rootElement;
        }
        private void BindAggregatorField(PropertyField delegateType, PropertyField aggregator) {
            delegateType.RegisterCallback<ChangeEvent<SerializableType>>(evt => UpdateAggregatorFieldState(evt.newValue, aggregator));
            UpdateAggregatorFieldState(((ActionDefinitionAsset)serializedObject.targetObject).delegateType, aggregator);
        }
        private void UpdateAggregatorFieldState(SerializableType type, PropertyField aggregator) {
            var returnType = type.IsCreated ? type.Value.GetMethod("Invoke").ReturnType : typeof(void);
            aggregator.visible = returnType != typeof(void);
        }
        private void UpdateAssemblyData() {
            if (serializedObject.targetObject is ActionDefinitionAsset definitionAsset) {
                var assemblyData = ProjectUtility.GetOrCreateProjectAsset<ActionAssemblyData>();
                assemblyData.entries[definitionAsset.id.guid] = new ActionAssemblyData.Entry(definitionAsset);
                ProjectUtility.UpdateProjectAsset(assemblyData);
            }
        }

    }
}