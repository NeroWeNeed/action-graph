using System;
using NeroWeNeed.Commons;
using UnityEngine;
namespace NeroWeNeed.ActionGraph.Editor {
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
    }
}