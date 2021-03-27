using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;


namespace NeroWeNeed.ActionGraph.Editor {
    public class SystemProducer {
        public const string GeneratedNamespaceExtension = "Generated";
        public const string ActionExecutionSystemBaseName = "ActionExecutionSystem";
        public static void CreateAssembly() {
            var definitions = AssetDatabase.FindAssets($"t:{nameof(ActionDefinitionAsset)}").Select(guid => AssetDatabase.LoadAssetAtPath<ActionDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid))).ToList();
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory("Library/ScriptAssemblies");
            var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("NeroWeNeed.ActionGraph.ActionSystems", new Version(1, 0, 0, 0)), "NeroWeNeed.ActionGraph.ActionSystems", new ModuleParameters { Kind = ModuleKind.Dll, AssemblyResolver = resolver });
            var call = typeof(SystemProducer).GetMethod(nameof(CreateAssembly), BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var definition in definitions) {
                if (definition.delegateType.IsCreated) {
                    call.MakeGenericMethod(definition.delegateType.Value).Invoke(null, new object[] { definition, assembly, assembly.MainModule });
                }
            }
        }
        private static string GetNamespace(string baseNamespace) => string.IsNullOrEmpty(baseNamespace) ? GeneratedNamespaceExtension : $"{baseNamespace}.{GeneratedNamespaceExtension}";
        private static void CreateAssembly<TDelegate>(ActionDefinitionAsset definitionAsset, AssemblyDefinition assemblyDefinition, ModuleDefinition moduleDefinition) where TDelegate : Delegate {

            var signature = typeof(TDelegate).GetMethod("Invoke");
            List<ComponentType> queryTypes = new List<ComponentType>() { ComponentType.ReadOnly<ActionExecutionRequest<TDelegate>>() };
            if (signature.ReturnType != typeof(void)) {
                queryTypes.Add(new ComponentType(typeof(ActionResult<,>).MakeGenericType(typeof(TDelegate), signature.ReturnType), ComponentType.AccessMode.ReadWrite));
            }
            if (definitionAsset.variableType.IsCreated) {
                queryTypes.Add(new ComponentType(typeof(ActionVariable<,>).MakeGenericType(typeof(TDelegate), definitionAsset.variableType.Value), ComponentType.AccessMode.ReadOnly));
            }

            var systemType = new TypeDefinition(GetNamespace(typeof(TDelegate).Namespace), $"{ActionExecutionSystemBaseName}_{typeof(TDelegate).FullName.Replace('.', '_')}", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.SequentialLayout);
            var systemBaseReference = moduleDefinition.ImportReference(typeof(ISystemBase));
            var systemStateReference = moduleDefinition.ImportReference(typeof(SystemState));
            systemType.Interfaces.Add(new InterfaceImplementation(systemBaseReference));
            systemType.Fields.Add(new FieldDefinition("query", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(typeof(EntityQuery))));


        }
        private static MethodDefinition GenerateOnCreate<TDelegate>(
            ActionDefinitionAsset definitionAsset,
            AssemblyDefinition assemblyDefinition,
            ModuleDefinition moduleDefinition,
            TypeReference systemStateReference,
            List<ComponentType> componentTypes
            ) where TDelegate : Delegate {
            var methodDefinition = new MethodDefinition(nameof(ISystemBase.OnCreate), Mono.Cecil.MethodAttributes.Public, moduleDefinition.TypeSystem.Void);
            methodDefinition.Parameters.Add(new ParameterDefinition(new ByReferenceType(systemStateReference)));


            var ilProcessor = methodDefinition.Body.GetILProcessor();

            return methodDefinition;
        }
        private static void GenerateILOnCreate<TDelegate>(ActionDefinitionAsset actionDefinition, ModuleDefinition moduleDefinition, ILProcessor processor, bool hasVariable, bool hasReturn, Type variableType, Type returnType, FieldDefinition queryField) where TDelegate : Delegate {
            var componentTypeReference = moduleDefinition.ImportReference(typeof(ComponentType));
            var components = actionDefinition.GetComponents();
            var queryComponentCount = 1;
            if (hasVariable)
                queryComponentCount++;
            if (hasReturn)
                queryComponentCount++;
            if (components != null) {
                queryComponentCount += components.Count;
            }
            processor.Emit(OpCodes.Nop);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldarg_1);
            processor.Emit(OpCodes.Ldc_I4, queryComponentCount); ;
            processor.Emit(OpCodes.Newarr, componentTypeReference);
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentType).GetMethod(nameof(ComponentType.ReadOnly)).MakeGenericMethod(typeof(ActionExecutionRequest<TDelegate>))));
            processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
            int offset = 1;
            if (hasVariable) {
                processor.Emit(OpCodes.Ldc_I4, offset++);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentType).GetMethod(nameof(ComponentType.ReadOnly)).MakeGenericMethod(typeof(ActionVariable<,>).MakeGenericType(typeof(TDelegate), variableType))));
                processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
            }
            if (hasReturn) {
                processor.Emit(OpCodes.Ldc_I4, offset++);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentType).GetMethod(nameof(ComponentType.ReadWrite)).MakeGenericMethod(typeof(ActionResult<,>).MakeGenericType(typeof(TDelegate), returnType))));
                processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
            }
            if (components != null) {
                foreach (var component in components)
                {
                    processor.Emit(OpCodes.Ldc_I4, offset++);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentType).GetMethod(nameof(ComponentType.ReadOnly)).MakeGenericMethod(component.Value.componentType)));
                    processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
                }
            }
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemState).GetMethod(nameof(SystemState.GetEntityQuery))));
            processor.Emit(OpCodes.Stfld, queryField);
            processor.Emit(OpCodes.Ldarg_1);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, queryField);
            processor.Emit(OpCodes.Stloc);
            processor.Emit(OpCodes.Ldloca);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemState).GetMethod(nameof(SystemState.RequireForUpdate))));
            processor.Emit(OpCodes.Nop);
            processor.Emit(OpCodes.Ldarg_1);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemState).GetMethod(nameof(SystemState.RequireSingletonForUpdate)).MakeGenericMethod(typeof(ActionIndex<TDelegate>))));
            processor.Emit(OpCodes.Nop);
            processor.Emit(OpCodes.Ret);
        }
    }

}