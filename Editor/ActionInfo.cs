using System;
namespace NeroWeNeed.ActionGraph.Editor {
    public struct ActionInfo {
        public ActionId id;
        public Type @delegate;
        public string name;
        public bool fieldOperations;
        public ActionInfo(ActionDefinitionAsset definitionAsset) {
            id = definitionAsset.id;
            @delegate = definitionAsset.delegateType;
            name = definitionAsset.Name;
            fieldOperations = definitionAsset.useFieldOperations;
        }
    }
}