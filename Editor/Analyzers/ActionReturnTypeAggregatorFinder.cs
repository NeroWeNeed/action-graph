using System.Collections.Generic;
using Mono.Cecil;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.AssemblyAnalyzers.Editor;
using NeroWeNeed.Commons.Editor;
using UnityEngine;

[assembly: Analyzer(typeof(NeroWeNeed.ActionGraph.Editor.Analyzer.ActionReturnTypeAggregatorFinder))]
namespace NeroWeNeed.ActionGraph.Editor.Analyzer {
    public class ActionReturnTypeAggregatorFinder : IMethodAnalyzer, IEndAnalysis, IBeginAnalysis {
        private Dictionary<string, List<SerializableMethod>> data;
        private string assembly;
        internal ActionReturnTypeAggregatorSchema schema;
        private TypeReference voidReference;
        public void OnBeginAnalysis(AssemblyDefinition assemblyDefinition) {
            assembly = assemblyDefinition.FullName;
            data = new Dictionary<string, List<SerializableMethod>>();
            voidReference = assemblyDefinition.MainModule.ImportReference(typeof(void));
        }
        public void OnEndAnalysis(AssemblyDefinition assemblyDefinition) {
            if (schema == null) {
                schema = ProjectUtility.GetOrCreateProjectAsset<ActionReturnTypeAggregatorSchema>();
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

        public void Analyze(AssemblyDefinition assemblyDefinition, ModuleDefinition moduleDefinition, TypeDefinition typeDefinition, MethodDefinition methodDefinition) {
            var method = new SerializableMethod(methodDefinition.DeclaringType.AssemblyQualifiedName(), methodDefinition.Name);
            var returnType = methodDefinition.ReturnType.Resolve();
            if (!data.TryGetValue(returnType.AssemblyQualifiedName(), out List<SerializableMethod> methods)) {
                methods = new List<SerializableMethod>();
                data[returnType.AssemblyQualifiedName()] = methods;
            }
            methods.Add(method);

        }



        public bool IsValid(MethodDefinition definition) => definition.ReturnType.IsPrimitive && definition.Parameters.Count == 2 && definition.Parameters[0].ParameterType.FullName == definition.ReturnType.FullName && definition.Parameters[1].ParameterType.FullName == definition.ReturnType.FullName;

        public bool IsExplorable(AssemblyDefinition assemblyDefinition, ModuleDefinition moduleDefinition, TypeDefinition typeDefinition) {
            return typeDefinition.IsAbstract && typeDefinition.IsSealed;
        }
    }
}