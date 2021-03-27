using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.AssemblyAnalyzers.Editor;
using NeroWeNeed.Commons.Editor;
using Unity.Entities;
using UnityEngine;
[assembly: Analyzer(typeof(NeroWeNeed.ActionGraph.Editor.Analyzer.ActionArgumentComponentFinder))]
namespace NeroWeNeed.ActionGraph.Editor.Analyzer {
    public class ActionArgumentComponentFinder : ITypeAnalyzer, IBeginAnalysis, IEndAnalysis {
        private Dictionary<string, Dictionary<string, ActionArgumentComponentSchema.ActionArgumentComponent>> data;
        private string assembly;
        internal ActionArgumentComponentSchema schema;
        public void Analyze(AssemblyDefinition assemblyDefinition, ModuleDefinition moduleDefinition, TypeDefinition typeDefinition) {
            var attr = typeDefinition.GetAttribute<ActionArgumentAttribute>();
            var delegateTypeReference = assemblyDefinition.MainModule.ImportReference(typeof(Delegate));
            var multicastDelegateTypeReference = assemblyDefinition.MainModule.ImportReference(typeof(MulticastDelegate));
            var actionDelegateTypeDefinition = ((TypeReference)attr.ConstructorArguments[0].Value).Resolve();
            var delegateType = new SerializableType(actionDelegateTypeDefinition.AssemblyQualifiedName());
            var parameterName = (string)attr.ConstructorArguments[1].Value;
            var field = typeDefinition.Fields[0];
            if (actionDelegateTypeDefinition.BaseType.FullName != delegateTypeReference.FullName && actionDelegateTypeDefinition.BaseType.FullName != multicastDelegateTypeReference.FullName) {
                Debug.LogError("Not Delegate");
                return;
            }
            var parameter = actionDelegateTypeDefinition.Methods.First(m => m.Name == "Invoke").Parameters.FirstOrDefault(parameter => parameter.Name == parameterName);
            if (parameter == null || parameter.ParameterType.FullName != field.FieldType.FullName) {
                Debug.LogError("Unknown parameter");
                return;
            }
            if (!data.TryGetValue(delegateType.AssemblyQualifiedName, out Dictionary<string, ActionArgumentComponentSchema.ActionArgumentComponent> argumentComponents)) {
                argumentComponents = new Dictionary<string, ActionArgumentComponentSchema.ActionArgumentComponent>();
                data[delegateType.AssemblyQualifiedName] = argumentComponents;
            }
            argumentComponents[parameterName] = new ActionArgumentComponentSchema.ActionArgumentComponent
            {
                delegateType = delegateType,
                componentType = new SerializableType(typeDefinition.AssemblyQualifiedName()),
                parameterName = parameterName,
                fieldName = field.Name,
                singletonTarget = parameter.CustomAttributes.Any(a => a.AttributeType.FullName == typeof(SingletonArgumentAttribute).FullName)
            };

        }

        public bool IsValid(TypeDefinition definition) {
            return definition.HasAttribute<ActionArgumentAttribute>() && definition.IsValueType && (definition.HasInterface<IComponentData>() || definition.HasInterface<ISystemStateComponentData>()) && definition.Fields.Count == 1;
        }

        public void OnBeginAnalysis(AssemblyDefinition assemblyDefinition) {
            assembly = assemblyDefinition.FullName;
            data = new Dictionary<string, Dictionary<string, ActionArgumentComponentSchema.ActionArgumentComponent>>();
        }

        public void OnEndAnalysis(AssemblyDefinition assemblyDefinition) {
            if (schema == null) {
                schema = ProjectUtility.GetOrCreateProjectAsset<ActionArgumentComponentSchema>();
            }

            if (schema != null) {
                if (data.IsEmpty()) {
                    schema.assemblyData.Remove(assembly);
                }
                else {
                    schema.assemblyData[assembly] = data;
                }
                ProjectUtility.UpdateProjectAsset(schema);
            }
        }
    }
}