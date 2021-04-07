using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NeroWeNeed.ActionGraph.Editor.CodeGen;
using NeroWeNeed.ActionGraph.Editor.Graph;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
namespace NeroWeNeed.ActionGraph.Editor {


    [CreateAssetMenu(fileName = "ActionDefinitionAsset", menuName = "Actions/Action Definition Asset", order = 0)]
    public class ActionDefinitionAsset : ScriptableObject {
        public static ActionDefinitionAsset Load(Type type) => AssetDatabase.FindAssets($"t:{nameof(ActionDefinitionAsset)}").Select(guid => AssetDatabase.LoadAssetAtPath<ActionDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid))).FirstOrDefault(asset => asset.delegateType == type);
        public static bool Load(Type type, out ActionDefinitionAsset actionDefinitionAsset) {
            actionDefinitionAsset = AssetDatabase.FindAssets($"t:{nameof(ActionDefinitionAsset)}").Select(guid => AssetDatabase.LoadAssetAtPath<ActionDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid))).FirstOrDefault(asset => asset.delegateType == type);
            return actionDefinitionAsset != null;
        }
        public static ActionDefinitionAsset Load(ActionId id) => AssetDatabase.FindAssets($"t:{nameof(ActionDefinitionAsset)}").Select(guid => AssetDatabase.LoadAssetAtPath<ActionDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid))).FirstOrDefault(asset => asset.id == id);
        public static IEnumerable<ActionDefinitionAsset> LoadAll() => AssetDatabase.FindAssets($"t:{nameof(ActionDefinitionAsset)}").Select(guid => AssetDatabase.LoadAssetAtPath<ActionDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid)));
        public static bool Load(ActionId id, out ActionDefinitionAsset actionDefinitionAsset) {
            actionDefinitionAsset = AssetDatabase.FindAssets($"t:{nameof(ActionDefinitionAsset)}").Select(guid => AssetDatabase.LoadAssetAtPath<ActionDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid))).FirstOrDefault(asset => asset.id == id);
            return actionDefinitionAsset != null;
        }
        [HideInInspector]
        public ActionId id = ActionId.Create();
        [Delayed]
        public string displayName;
        public string Name { get => string.IsNullOrEmpty(displayName) ? delegateType.Value?.Name : displayName; }
        [SuperTypeFilter(typeof(Delegate))]
        [ConcreteTypeFilter]
        [ExcludeAssemblyFilter("mscorlib")]
        [ExcludeAssemblyFilter("System.", MatchType.StartsWith)]
        [ExcludeTypeNameFilter("System.", MatchType.StartsWith)]
        public SerializableType delegateType;
        [UnmanagedFilter]
        [ConcreteTypeFilter]
        [AttributeTypeFilter(typeof(ActionVariable))]
        [ExcludeAssemblyFilter("mscorlib")]
        [ExcludeAssemblyFilter("System.", MatchType.StartsWith)]
        [ExcludeTypeNameFilter("System.", MatchType.StartsWith)]
        public SerializableType variableType;
        [SuperTypeFilter(typeof(ActionValidationRule))]
        [ConcreteTypeFilter]
        public SerializableType validatorType;
        public bool useFieldOperations = true;
        [Provider(typeof(ActionDefinitionReturnTypeAggregatorProvider))]
        public SerializableMethod aggregator;
        [SuperTypeFilter(typeof(EntityCommandBufferSystem))]
        public SerializableType destroyEntitiesUsing = typeof(EndSimulationEntityCommandBufferSystem);
        public Dictionary<string, ActionArgumentComponentSchema.ActionArgumentComponent> GetComponents() {
            if (ProjectUtility.GetOrCreateProjectAsset<ActionArgumentComponentSchema>().data.TryGetValue(delegateType.Value, out var value)) {
                return value;
            }
            else {
                return null;
            }
        }
        private void OnValidate() {
            if (string.IsNullOrWhiteSpace(displayName)) {
                displayName = $"Action({id})";
            }
        }
    }
}