using System.Collections.Generic;
using Mono.Cecil;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.AssemblyAnalyzers.Editor;
using NeroWeNeed.Commons.Editor;
using UnityEngine;
[assembly: Analyzer(typeof(NeroWeNeed.ActionGraph.Editor.Analyzer.FieldOperationFinder))]


namespace NeroWeNeed.ActionGraph.Editor.Analyzer {
    public class FieldOperationFinder : IMethodAnalyzer, IEndAnalysis, IBeginAnalysis {
        private Dictionary<string, FieldOperationSchema.Operation> operations;
        private string assembly;
        internal FieldOperationSchema schema;
        public void Analyze(AssemblyDefinition assemblyDefinition, ModuleDefinition moduleDefinition, TypeDefinition typeDefinition, MethodDefinition methodDefinition) {
            var attr = methodDefinition.GetAttribute<FieldOperationAttribute>();
            if (attr.ConstructorArguments.Count != 5)
                return;
            string identifier = (string)attr.ConstructorArguments[0].Value;
            TypeDefinition configTypeDef = ((TypeReference)attr.ConstructorArguments[1].Value).Resolve();
            string displayName = (string)attr.ConstructorArguments[2].Value;
            string subIdentifier = (string)attr.ConstructorArguments[3].Value;
            TypeDefinition outputTypeRef = ((TypeReference)attr.ConstructorArguments[4].Value).Resolve();
            var outputTypeQualifiedName = outputTypeRef.AssemblyQualifiedName();
            var configTypeQualifiedName = configTypeDef?.AssemblyQualifiedName();
            var configType = new SerializableType(configTypeQualifiedName);
            var outputType = new SerializableType(outputTypeQualifiedName);
            var method = new SerializableMethod(typeDefinition.AssemblyQualifiedName(), methodDefinition.Name);
            var opCall = new FieldOperationSchema.Operation.Variant
            {
                displayName = displayName,
                configType = configType,
                method = method,
                outputType = outputType
            };
            if (!operations.TryGetValue(identifier, out FieldOperationSchema.Operation op)) {
                op = new FieldOperationSchema.Operation
                {
                    identifier = identifier
                };
                operations[identifier] = op;
            }
            if (op.callVariants.ContainsKey(subIdentifier)) {
                Debug.LogError($"Duplicate Sub-Identifiers found for identifier '{identifier}': '{subIdentifier}'");
            }
            op.callVariants[subIdentifier] = opCall;
        }
        public bool IsExplorable(AssemblyDefinition assembly, ModuleDefinition moduleDefinition, TypeDefinition type) {
            return type.IsAbstract && type.IsSealed;
        }

        public bool IsValid(MethodDefinition definition) => definition.HasAttribute<FieldOperationAttribute>();

        public void OnBeginAnalysis(AssemblyDefinition assemblyDefinition) {
            assembly = assemblyDefinition.FullName;
            operations = new Dictionary<string, FieldOperationSchema.Operation>();
        }
        public void OnEndAnalysis(AssemblyDefinition assemblyDefinition) {
            if (schema == null) {
                schema = ProjectUtility.GetProjectAsset<FieldOperationSchema>();
            }
            if (schema != null) {
                if (operations.IsEmpty()) {
                    schema.assemblyData.Remove(assembly);
                }
                else {
                    schema.assemblyData[assembly] = operations;
                }
                schema.UpdateIndices();
                ProjectUtility.UpdateProjectAsset(schema);
            }
        }
    }
}