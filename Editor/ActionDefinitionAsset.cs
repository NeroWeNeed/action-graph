using System;
using System.Collections.Generic;
using System.Linq;
using NeroWeNeed.ActionGraph.Editor.Graph;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
namespace NeroWeNeed.ActionGraph.Editor {


    [CreateAssetMenu(fileName = "ActionDefinitionAsset", menuName = "Actions/Action Definition Asset", order = 0)]
    public class ActionDefinitionAsset : ScriptableObject {

        [InitializeOnLoadMethod]
        public static void RebuildActionLists() {
            foreach (var definition in AssetDatabase.FindAssets($"t:{nameof(ActionDefinitionAsset)}").Select(guid => AssetDatabase.LoadAssetAtPath<ActionDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid)))) {
                definition.recreateActionList = true;
                EditorUtility.SetDirty(definition);
            }
        }
        [HideInInspector]
        public ActionId id = ActionId.Create();

        public string displayName;
        public string associatedDirectory;
        public string Name { get => string.IsNullOrEmpty(displayName) ? delegateType.Value?.Name : displayName; }
        [SuperTypeFilter(typeof(Delegate))]
        [ConcreteTypeFilter]
        [ExcludeAssemblyFilter("mscorlib")]
        [ExcludeAssemblyFilter("System.", MatchType.StartsWith)]
        public SerializableType delegateType;
        [UnmanagedFilter]
        [ConcreteTypeFilter]
        [AttributeTypeFilter(typeof(ActionVariable))]
        [ExcludeAssemblyFilter("mscorlib")]
        [ExcludeAssemblyFilter("System.",MatchType.StartsWith)]
        public SerializableType variableType;
        [SuperTypeFilter(typeof(ActionValidationRule))]
        [ConcreteTypeFilter]
        public SerializableType validatorType;
        public bool useFieldOperations = true;
        [HideInInspector]
        public bool recreateActionList = true;
        [Provider(typeof(ActionReturnTypeAggregatorProvider))]
        public SerializableMethod aggregator;
        [SuperTypeFilter(typeof(EntityCommandBufferSystem))]
        public SerializableType destroyEntitiesUsing = typeof(EndSimulationEntityCommandBufferSystem);

        [HideInInspector]
        private List<Action> actions = new List<Action>();
        public List<Action> GetActions() {
            if (recreateActionList) {
                RecreateActionList();

            }
            return actions;
        }
        public Dictionary<string, ActionArgumentComponentSchema.ActionArgumentComponent> GetComponents() {
            if (ProjectUtility.GetOrCreateProjectAsset<ActionArgumentComponentSchema>().data.TryGetValue(delegateType.Value, out var value)) {
                return value;
            }
            else {
                return null;
            }
        }
        public void RecreateActionList() {
            var schema = ProjectUtility.GetProjectAsset<ActionSchema>();
            if (schema.data.TryGetValue(delegateType.Value, out var data)) {
                actions = data.SelectMany(kv => kv.Value.variants.Select(method => new Action(kv.Key, method.Key, method.Value.config, method.Value.call))).ToList();
                actions.Sort();
                EditorUtility.SetDirty(this);

            }
        }
        private void OnValidate() {
            if (string.IsNullOrWhiteSpace(displayName)) {
                displayName = $"Action({id})";
            }
            if (string.IsNullOrWhiteSpace(associatedDirectory)) {
                associatedDirectory = $"Assets/Actions/{displayName}";
            }
            if (associatedDirectory.EndsWith("/")) {
                associatedDirectory = associatedDirectory.Substring(0, associatedDirectory.Length - 1);
            }
        }
        [Serializable]
        public struct Action : IComparable<Action> {
            public string identifier;
            public string subIdentifier;
            public SerializableType config;
            public SerializableMethod method;

            public Action(string identifier, string subIdentifier, SerializableType config, SerializableMethod method) {
                this.identifier = identifier;
                this.subIdentifier = subIdentifier;
                this.method = method;
                this.config = config;
            }

            public int CompareTo(Action other) {
                var c1 = this.identifier.CompareTo(other.identifier);
                return c1 == 0 ? this.subIdentifier.CompareTo(other.subIdentifier) : c1;
            }
        }

    }
}