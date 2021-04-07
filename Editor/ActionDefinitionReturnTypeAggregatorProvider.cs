using System.Collections.Generic;
using System.Reflection;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using UnityEditor;

namespace NeroWeNeed.ActionGraph.Editor {
    public class ActionDefinitionReturnTypeAggregatorProvider : IMethodProvider {
        public List<SerializableMethod> GetMethods(FieldInfo fieldInfo, SerializedProperty property) {
            if (property.serializedObject.targetObject is ActionDefinitionAsset definitionAsset && definitionAsset.delegateType.IsCreated) {
                var returnType = definitionAsset.delegateType.Value.GetMethod("Invoke").ReturnType;
                if (returnType != typeof(void) && ProjectUtility.GetOrCreateProjectAsset<ActionReturnTypeAggregatorSchema>().data.TryGetValue(returnType, out var result)) {
                    return result;
                }
            }
            return null;
        }
    }
}