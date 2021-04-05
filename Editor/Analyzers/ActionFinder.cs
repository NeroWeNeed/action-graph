using System.Collections.Generic;
using Mono.Cecil;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.AssemblyAnalyzers.Editor;
using NeroWeNeed.Commons.Editor;
using UnityEngine;
[assembly: Analyzer(typeof(NeroWeNeed.ActionGraph.Editor.Analyzer.ActionFinder))]

namespace NeroWeNeed.ActionGraph.Editor.Analyzer {
    public class ActionFinder : IMethodAnalyzer, IEndAnalysis, IBeginAnalysis {
        private Dictionary<string, ActionSchema.Action> actions;
        private string assembly;
        internal ActionSchema schema;
        public void Analyze(AssemblyDefinition assemblyDefinition, ModuleDefinition moduleDefinition, TypeDefinition typeDefinition, MethodDefinition methodDefinition) {
            var attr = methodDefinition.GetAttribute<ActionAttribute>();
            if (attr.ConstructorArguments.Count < 2)
                return;
            TypeDefinition actionTypeDef = ((TypeReference)attr.ConstructorArguments[0].Value).Resolve();
            string identifier = (string)attr.ConstructorArguments[1].Value;
            TypeDefinition configTypeDef = attr.ConstructorArguments.Count >= 3 ? ((TypeReference)attr.ConstructorArguments[2].Value).Resolve() : null;
            string displayName = attr.ConstructorArguments.Count >= 4 ? (string)attr.ConstructorArguments[3].Value : null;
            string subIdentifier = attr.ConstructorArguments.Count == 5 ? (string)attr.ConstructorArguments[4].Value : string.Empty;
            if (string.IsNullOrEmpty(subIdentifier)) {
                subIdentifier = ActionSchema.DefaultSubIdentifier;
            }
            var actionTypeQualifiedName = actionTypeDef.FullName + ", " + actionTypeDef.Module.Assembly.FullName;
            var configTypeQualifiedName = configTypeDef == null ? null : configTypeDef.FullName + ", " + configTypeDef.Module.Assembly.FullName;
            var configType = new SerializableType(configTypeQualifiedName);
            var method = new SerializableMethod(typeDefinition.AssemblyQualifiedName(), methodDefinition.Name);
            var actionMethod = new ActionSchema.Action.Variant
            {
                config = configType,
                call = method
            };
            if (!actions.TryGetValue(identifier, out ActionSchema.Action action)) {
                action = new ActionSchema.Action
                {
                    identifier = identifier,
                    displayName = displayName,
                    action = new SerializableType(actionTypeQualifiedName)
                };
                actions[identifier] = action;
            }
            if (action.variants.ContainsKey(subIdentifier)) {
                Debug.LogError($"Duplicate Sub-Identifiers found for identifier '{identifier}': '{subIdentifier}'");
            }
            action.variants[subIdentifier] = actionMethod;
        }
        public bool IsExplorable(AssemblyDefinition assembly, ModuleDefinition moduleDefinition, TypeDefinition type) {
            return type.IsAbstract && type.IsSealed;
        }

        public bool IsValid(MethodDefinition definition) => definition.HasAttribute<ActionAttribute>();

        public void OnBeginAnalysis(AssemblyDefinition assemblyDefinition) {
            assembly = assemblyDefinition.FullName;
            actions = new Dictionary<string, ActionSchema.Action>();
        }



        public void OnEndAnalysis(AssemblyDefinition assemblyDefinition) {
            if (schema == null) {
                schema = ProjectUtility.GetProjectAsset<ActionSchema>();
            }
            if (schema != null) {
                if (actions.IsEmpty()) {
                    schema.assemblyData.Remove(assembly);
                }
                else {
                    schema.assemblyData[assembly] = actions;
                }
                ProjectUtility.UpdateProjectAsset(schema);
            }
        }
    }
}