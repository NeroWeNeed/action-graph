using System;
using NeroWeNeed.Commons;
using UnityEngine;
namespace NeroWeNeed.ActionGraph.Editor {

    [CreateAssetMenu(fileName = "ActionDefinitionAsset", menuName = "Actions/Action Definition Asset", order = 0)]
    public class ActionDefinitionAsset : ScriptableObject {
        [HideInInspector]
        public ActionId id = ActionId.Create();
        [Delayed]
        public new string name;
        public string associatedDirectory;
        public string Name { get => string.IsNullOrEmpty(name) ? delegateType.Value?.Name : name; }
        [SuperTypeFilter(typeof(Delegate))]
        [ConcreteTypeFilter]
        public SerializableType delegateType;
        [UnmanagedFilter]
        [ConcreteTypeFilter]
        public SerializableType variableType;
        [SuperTypeFilter(typeof(ActionValidationRule))]
        [ConcreteTypeFilter]
        public SerializableType validatorType;
        private void OnValidate() {
            if (string.IsNullOrWhiteSpace(name)) {
                name = $"Action({id})";
            }
            if (string.IsNullOrWhiteSpace(associatedDirectory)) {
                associatedDirectory = $"Assets/Actions/{name}";
            }
            if (associatedDirectory.EndsWith("/")) {
                associatedDirectory = associatedDirectory.Substring(0, associatedDirectory.Length - 1);
            }
        }

    }
}