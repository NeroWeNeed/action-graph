using System.Collections.Generic;
using Mono.Cecil;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.AssemblyAnalyzers.Editor;
using NeroWeNeed.Commons.Editor;
using UnityEngine;
[assembly: Analyzer(typeof(NeroWeNeed.ActionGraph.Editor.Analyzer.NodeLayoutHandlerFinder))]
namespace NeroWeNeed.ActionGraph.Editor.Analyzer {
    public class NodeLayoutHandlerFinder : ITypeAnalyzer, IEndAnalysis, IBeginAnalysis {
        private Dictionary<string, SerializableType> handlers;
        private string assembly;
        internal NodeLayoutHandlerSchema schema;
        public void Analyze(AssemblyDefinition assemblyDefinition, ModuleDefinition moduleDefinition, TypeDefinition typeDefinition) {
            var attr = typeDefinition.GetAttribute<NodeLayoutHandlerAttribute>();
            var targetType = ((TypeReference)attr.ConstructorArguments[0].Value).Resolve();
            handlers[targetType.AssemblyQualifiedName()] = new SerializableType(typeDefinition.AssemblyQualifiedName());
        }

        public bool IsValid(TypeDefinition definition) {
            return definition.HasAttribute<NodeLayoutHandlerAttribute>() && !definition.IsAbstract;
        }
        public void OnBeginAnalysis(AssemblyDefinition assemblyDefinition) {
            assembly = assemblyDefinition.FullName;
            handlers = new Dictionary<string, SerializableType>();
        }
        public void OnEndAnalysis(AssemblyDefinition assemblyDefinition) {
            if (schema == null) {
                schema = ProjectUtility.GetOrCreateProjectAsset<NodeLayoutHandlerSchema>();
            }

            if (schema != null) {
                if (handlers.IsEmpty()) {
                    schema.assemblyData.Remove(assembly);
                }
                else {
                    schema.assemblyData[assembly] = handlers;
                }
                ProjectUtility.UpdateProjectAsset(schema);
            }
        }
    }
}