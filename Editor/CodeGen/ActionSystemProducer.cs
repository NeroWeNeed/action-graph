using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using NeroWeNeed.Commons.Editor;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEngine;

namespace NeroWeNeed.ActionGraph.Editor.CodeGen {
    public static class ActionSystemProducerClass {
        public const string GeneratedNamespaceExtension = "Generated";
        public const string ActionExecutionSystemBaseName = "ActionExecutionSystem";
        public const string AssemblyName = "NeroWeNeed.ActionGraph.ActionSystems";
        public const string Output = "Packages/github.neroweneed.action-graph/" + AssemblyName + ".dll";
        [MenuItem("Assets/Generate Action Assembly")]
        public static void CreateAssembly() {
            var definitions = AssetDatabase.FindAssets($"t:{nameof(ActionDefinitionAsset)}").Select(guid => AssetDatabase.LoadAssetAtPath<ActionDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid))).ToList();
            using var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory("Library/ScriptAssemblies");
            using (var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(AssemblyName, new Version(1, 0, 0, 0)), "NeroWeNeed.ActionGraph.ActionSystems", new ModuleParameters { Kind = ModuleKind.Dll, AssemblyResolver = resolver, Runtime = TargetRuntime.Net_4_0, })) {
                var generics = new HashSet<ActionExecutionSystemDefinition.GenericAttributeInfo>();
                var typeReferenceType = assembly.MainModule.ImportReference(typeof(Type));
                var groupType = assembly.MainModule.ImportReference(typeof(ActionExecutionSystemGroup));
                var groupConstructor = assembly.MainModule.ImportReference(typeof(UpdateInGroupAttribute).GetConstructor(new Type[] { typeof(Type) }));
                foreach (var definition in definitions) {
                    if (definition.delegateType.IsCreated) {
                        var system = ActionExecutionSystemDefinition.Create(assembly.MainModule, definition, definition.delegateType);
                        foreach (var generic in system.generics) {
                            generics.Add(generic);
                        }
                        var attr = new CustomAttribute(groupConstructor);
                        attr.ConstructorArguments.Add(new CustomAttributeArgument(typeReferenceType, groupType));
                        system.definition.CustomAttributes.Add(attr);
                        assembly.MainModule.Types.Add(system.definition);
                    }
                }
                foreach (var generic in generics) {
                    var attr = new CustomAttribute(generic.constructor);
                    attr.ConstructorArguments.Add(new CustomAttributeArgument(typeReferenceType, assembly.MainModule.ImportReference(generic.type)));
                    assembly.CustomAttributes.Add(attr);
                }
                assembly.Write(Output);
            }
            AssetDatabase.ImportAsset(Output);
        }
        private static string GetNamespace(string baseNamespace) => string.IsNullOrEmpty(baseNamespace) ? GeneratedNamespaceExtension : $"{baseNamespace}.{GeneratedNamespaceExtension}";
        private abstract class ActionExecutionSystemDefinition {
            public TypeDefinition definition;
            public FieldDefinition entityQueryField;
            public MethodDefinition onCreateMethod;
            public MethodDefinition onUpdateMethod;
            public MethodDefinition onDestroyMethod;
            public Type variableType;
            public TypeReference variableTypeReference;
            public bool HasVariable { get => variableType != null; }
            public List<GenericAttributeInfo> generics = new List<GenericAttributeInfo>();
            public static ActionExecutionSystemDefinition Create<TDelegate>(ModuleDefinition moduleDefinition, ActionDefinitionAsset actionDefinitionAsset) where TDelegate : Delegate {
                return new ActionExecutionSystemDefinition<TDelegate>(moduleDefinition, actionDefinitionAsset);
            }
            public static ActionExecutionSystemDefinition Create(ModuleDefinition moduleDefinition, ActionDefinitionAsset actionDefinitionAsset, Type @delegate) {
                return (ActionExecutionSystemDefinition)typeof(ActionExecutionSystemDefinition).GetGenericMethod("Create", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(@delegate).Invoke(null, new object[] { moduleDefinition, actionDefinitionAsset });
            }
            public struct GenericAttributeInfo : IEquatable<GenericAttributeInfo> {
                public Type type;
                public MethodReference constructor;
                public bool Equals(GenericAttributeInfo other) {
                    return EqualityComparer<Type>.Default.Equals(type, other.type);
                }
                public override int GetHashCode() {
                    int hashCode = -193800796;
                    hashCode = hashCode * -1521134295 + EqualityComparer<Type>.Default.GetHashCode(type);
                    return hashCode;
                }
                public static GenericAttributeInfo Component(ModuleDefinition moduleDefinition, Type type) {
                    return new GenericAttributeInfo
                    {
                        type = type,
                        constructor = moduleDefinition.ImportReference(typeof(RegisterGenericComponentTypeAttribute).GetConstructor(new Type[] { typeof(Type) }))
                    };
                }
                public static GenericAttributeInfo Job(ModuleDefinition moduleDefinition, Type type) {
                    return new GenericAttributeInfo
                    {
                        type = type,
                        constructor = moduleDefinition.ImportReference(typeof(RegisterGenericJobTypeAttribute).GetConstructor(new Type[] { typeof(Type) }))
                    };
                }
            }
        }
        private class ActionExecutionSystemDefinition<TDelegate> : ActionExecutionSystemDefinition where TDelegate : Delegate {
            public ActionExecutionJobDefinition jobDefinition;
            public ActionExecutionSystemDefinition(ModuleDefinition moduleDefinition, ActionDefinitionAsset actionDefinitionAsset) {
                definition = new TypeDefinition(GetNamespace(typeof(TDelegate).Namespace), $"{ActionExecutionSystemBaseName}_{typeof(TDelegate).FullName.Replace('.', '_')}", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Sealed, moduleDefinition.ImportReference(typeof(SystemBase)));
                entityQueryField = new FieldDefinition("query", Mono.Cecil.FieldAttributes.Private, moduleDefinition.ImportReference(typeof(EntityQuery)));
                definition.Fields.Add(entityQueryField);
                variableType = actionDefinitionAsset.variableType.IsCreated ? actionDefinitionAsset.variableType.Value : null;
                if (HasVariable) {
                    variableTypeReference = moduleDefinition.ImportReference(variableType);
                }
                GenerateConstructor(moduleDefinition);
                if (HasVariable) {
                    variableTypeReference = moduleDefinition.ImportReference(variableType);
                }
                GenerateJob(moduleDefinition, actionDefinitionAsset);
                GenerateOnCreate(moduleDefinition, actionDefinitionAsset);
                GenerateOnUpdate(moduleDefinition, actionDefinitionAsset);
                GenerateOnDestroy(moduleDefinition, actionDefinitionAsset);
                generics.Add(GenericAttributeInfo.Component(moduleDefinition, typeof(ActionExecutionRequest<TDelegate>)));
                generics.Add(GenericAttributeInfo.Component(moduleDefinition, typeof(ActionExecutionRequestAt<TDelegate>)));
                generics.Add(GenericAttributeInfo.Component(moduleDefinition, typeof(ActionIndex<TDelegate>)));
                generics.Add(GenericAttributeInfo.Job(moduleDefinition, typeof(ActionExecutionConfigInitJob<TDelegate>)));
                if (HasVariable) {
                    generics.Add(GenericAttributeInfo.Job(moduleDefinition, typeof(ActionExecutionApplyVariableJob<,>).MakeGenericType(typeof(TDelegate), variableType)));
                    generics.Add(GenericAttributeInfo.Component(moduleDefinition, typeof(ActionVariable<,>).MakeGenericType(typeof(TDelegate), variableType)));
                }
                if (jobDefinition.HasReturnType && jobDefinition.HasReturnTypeAggregator) {
                    generics.Add(GenericAttributeInfo.Component(moduleDefinition, typeof(ActionResult<,>).MakeGenericType(typeof(TDelegate), jobDefinition.returnType)));
                }
            }
            private void GenerateConstructor(ModuleDefinition moduleDefinition) {
                var ctor = new MethodDefinition(".ctor", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName, moduleDefinition.TypeSystem.Void);
                var il = ctor.Body.GetILProcessor();
                il.Emit(OpCodes.Ret);
                definition.Methods.Add(ctor);
            }
            private void GenerateOnCreate(ModuleDefinition moduleDefinition, ActionDefinitionAsset actionDefinitionAsset) {
                onCreateMethod = new MethodDefinition("OnCreate", Mono.Cecil.MethodAttributes.Family, moduleDefinition.TypeSystem.Void)
                {
                    IsVirtual = true,
                    IsReuseSlot = true,
                    IsHideBySig = true
                };
                definition.Methods.Add(onCreateMethod);
                var componentTypeReference = moduleDefinition.ImportReference(typeof(ComponentType));
                var components = actionDefinitionAsset.GetComponents();
                var queryComponentCount = 1;
                var variableType = actionDefinitionAsset.variableType.Value;
                var returnType = typeof(TDelegate).GetMethod("Invoke").ReturnType;
                var processor = onCreateMethod.Body.GetILProcessor();
                if (variableType != null)
                    queryComponentCount++;
                if (returnType != null)
                    queryComponentCount++;
                if (components != null) {
                    queryComponentCount += components.Count(c => !c.Value.singletonTarget);
                }
                processor.Emit(OpCodes.Nop);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldc_I4, queryComponentCount);
                processor.Emit(OpCodes.Newarr, componentTypeReference);
                processor.Emit(OpCodes.Dup);
                processor.Emit(OpCodes.Ldc_I4_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentType).GetGenericMethod(nameof(ComponentType.ReadOnly), BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(ActionExecutionRequest<TDelegate>))));
                processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
                int offset = 1;
                if (HasVariable) {
                    processor.Emit(OpCodes.Dup);
                    processor.Emit(OpCodes.Ldc_I4, offset++);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentType).GetGenericMethod(nameof(ComponentType.ReadOnly), BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(ActionVariable<,>).MakeGenericType(typeof(TDelegate), variableType))));
                    processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
                }
                if (returnType != null) {
                    processor.Emit(OpCodes.Dup);
                    processor.Emit(OpCodes.Ldc_I4, offset++);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentType).GetGenericMethod(nameof(ComponentType.ReadWrite), BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(ActionResult<,>).MakeGenericType(typeof(TDelegate), returnType))));
                    processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
                }
                if (components != null) {
                    foreach (var component in components.Where(c => !c.Value.singletonTarget)) {
                        processor.Emit(OpCodes.Dup);
                        processor.Emit(OpCodes.Ldc_I4, offset++);
                        processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentType).GetGenericMethod(nameof(ComponentType.ReadOnly), BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(component.Value.componentType)));
                        processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
                    }
                }
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).First(m => m.Name == "GetEntityQuery" && m.GetParameters().FirstOrDefault()?.GetCustomAttribute<ParamArrayAttribute>() != null && m.GetParameters().FirstOrDefault()?.ParameterType?.GetElementType() == typeof(ComponentType))));
                processor.Emit(OpCodes.Stfld, entityQueryField);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, entityQueryField);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.RequireForUpdate))));
                processor.Emit(OpCodes.Nop);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.RequireSingletonForUpdate)).MakeGenericMethod(typeof(ActionIndex<TDelegate>))));
                processor.Emit(OpCodes.Nop);
                foreach (var component in components.Where(c => c.Value.singletonTarget)) {
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.RequireSingletonForUpdate)).MakeGenericMethod(component.Value.componentType)));
                    processor.Emit(OpCodes.Nop);
                }
                processor.Emit(OpCodes.Ret);
            }
            private void GenerateOnUpdate(ModuleDefinition moduleDefinition, ActionDefinitionAsset actionDefinitionAsset) {
                onUpdateMethod = new MethodDefinition(nameof(ISystemBase.OnUpdate), Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.CheckAccessOnOverride, moduleDefinition.TypeSystem.Void)
                {
                    IsVirtual = true,
                    IsReuseSlot = true,
                    IsHideBySig = true
                };
                //onUpdateMethod.Parameters.Add(new ParameterDefinition(new ByReferenceType(moduleDefinition.ImportReference(typeof(SystemState)))));
                onUpdateMethod.Body.InitLocals = true;
                onUpdateMethod.Body.SimplifyMacros();
                definition.Methods.Add(onUpdateMethod);
                var processor = onUpdateMethod.Body.GetILProcessor();
                //Variables
                var handles = new VariableDefinition(moduleDefinition.ImportReference(typeof(NativeArray<ConfigInfo>)));
                onUpdateMethod.Body.Variables.Add(handles);
                var initConfigInfoJobHandle = new VariableDefinition(moduleDefinition.ImportReference(typeof(JobHandle)));
                onUpdateMethod.Body.Variables.Add(initConfigInfoJobHandle);
                var executeAction = new VariableDefinition(moduleDefinition.ImportReference(typeof(JobHandle)));
                onUpdateMethod.Body.Variables.Add(executeAction);
                var initConfigInfoJob = new VariableDefinition(moduleDefinition.ImportReference(typeof(ActionExecutionConfigInitJob<TDelegate>)));
                onUpdateMethod.Body.Variables.Add(initConfigInfoJob);
                var executeActionJob = new VariableDefinition(jobDefinition.definition);
                onUpdateMethod.Body.Variables.Add(executeActionJob);
                var executeActionJobHandle = new VariableDefinition(moduleDefinition.ImportReference(typeof(JobHandle)));
                onUpdateMethod.Body.Variables.Add(executeActionJobHandle);
                VariableDefinition applyVariableJobHandle = null, applyVariableJob = null;
                if (HasVariable) {
                    applyVariableJobHandle = new VariableDefinition(moduleDefinition.ImportReference(typeof(JobHandle)));
                    onUpdateMethod.Body.Variables.Add(applyVariableJobHandle);
                    applyVariableJob = new VariableDefinition(moduleDefinition.ImportReference(typeof(ActionExecutionApplyVariableJob<,>).MakeGenericType(typeof(TDelegate), variableType)));
                    onUpdateMethod.Body.Variables.Add(applyVariableJob);
                }
                //Methods
                var systemState_executionRequestHandle = moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetComponentTypeHandle)).MakeGenericMethod(typeof(ActionExecutionRequest<TDelegate>)));
                MethodReference systemState_variableRequestHandle = null;
                var systemState_dependency = moduleDefinition.ImportReference(typeof(SystemBase).GetProperty("Dependency", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetMethod);
                var initConfigJob_schedule = moduleDefinition.ImportReference(typeof(JobEntityBatchExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public).First(m => m.Name == nameof(JobEntityBatchExtensions.Schedule) && m.GetParameters().Length == 3).MakeGenericMethod(typeof(ActionExecutionConfigInitJob<TDelegate>)));
                var executeActionJob_schedule = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(JobEntityBatchExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public).First(m => m.Name == nameof(JobEntityBatchExtensions.Schedule) && m.GetParameters().Length == 3)));
                executeActionJob_schedule.GenericArguments.Add(jobDefinition.definition);
                MethodReference applyVariableJob_schedule = null;
                //Fields
                var initConfigInfo_requestHandle = moduleDefinition.ImportReference(typeof(ActionExecutionConfigInitJob<TDelegate>).GetField(nameof(ActionExecutionConfigInitJob<TDelegate>.requestHandle)));
                var initConfigInfo_handles = moduleDefinition.ImportReference(typeof(ActionExecutionConfigInitJob<TDelegate>).GetField(nameof(ActionExecutionConfigInitJob<TDelegate>.handles)));
                FieldReference applyVariable_requestHandle = null, applyVariable_handles = null, applyVariable_variables = null;
                if (HasVariable) {
                    var variableJobType = typeof(ActionExecutionApplyVariableJob<,>).MakeGenericType(typeof(TDelegate), variableType);
                    var variableComponentType = typeof(ActionVariable<,>).MakeGenericType(typeof(TDelegate), variableType);
                    applyVariable_requestHandle = moduleDefinition.ImportReference(variableJobType.GetField("requestHandle"));
                    applyVariable_handles = moduleDefinition.ImportReference(variableJobType.GetField("handles"));
                    applyVariable_variables = moduleDefinition.ImportReference(variableJobType.GetField("variableHandle"));
                    systemState_variableRequestHandle = moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetComponentTypeHandle)).MakeGenericMethod(variableComponentType));
                    applyVariableJob_schedule = moduleDefinition.ImportReference(typeof(JobEntityBatchExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public).First(m => m.Name == nameof(JobEntityBatchExtensions.Schedule) && m.GetParameters().Length == 3).MakeGenericMethod(variableJobType));
                }
                //Init Config Handle Job
                VariableDefinition currentDependency = null;
                processor.Emit(OpCodes.Nop);
                processor.Emit(OpCodes.Ldloca, handles);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldflda, entityQueryField);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(EntityQuery).GetMethod(nameof(EntityQuery.CalculateEntityCount), Type.EmptyTypes)));
                processor.Emit(OpCodes.Ldc_I4, (int)Allocator.TempJob);
                processor.Emit(OpCodes.Ldc_I4_1);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(NativeArray<ConfigInfo>).GetConstructor(new Type[] { typeof(int), typeof(Allocator), typeof(NativeArrayOptions) })));
                processor.Emit(OpCodes.Ldloca, initConfigInfoJob);
                processor.Emit(OpCodes.Initobj, moduleDefinition.ImportReference(typeof(ActionExecutionConfigInitJob<TDelegate>)));
                processor.Emit(OpCodes.Ldloca, initConfigInfoJob);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldc_I4_1);
                processor.Emit(OpCodes.Call, systemState_executionRequestHandle);
                processor.Emit(OpCodes.Stfld, initConfigInfo_requestHandle);
                processor.Emit(OpCodes.Ldloca, initConfigInfoJob);
                processor.Emit(OpCodes.Ldloc, handles);
                processor.Emit(OpCodes.Stfld, initConfigInfo_handles);
                processor.Emit(OpCodes.Ldloc, initConfigInfoJob);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, entityQueryField);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, systemState_dependency);
                processor.Emit(OpCodes.Call, initConfigJob_schedule);
                processor.Emit(OpCodes.Stloc, initConfigInfoJobHandle);
                currentDependency = initConfigInfoJobHandle;
                //Apply Variables Job
                if (HasVariable) {
                    processor.Emit(OpCodes.Ldloca, applyVariableJob);
                    processor.Emit(OpCodes.Initobj, moduleDefinition.ImportReference(typeof(ActionExecutionApplyVariableJob<,>).MakeGenericType(typeof(TDelegate), variableType)));
                    processor.Emit(OpCodes.Ldloca, applyVariableJob);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldc_I4_1);
                    processor.Emit(OpCodes.Call, systemState_executionRequestHandle);
                    processor.Emit(OpCodes.Stfld, applyVariable_requestHandle);
                    processor.Emit(OpCodes.Ldloca, applyVariableJob);
                    processor.Emit(OpCodes.Ldloc, handles);
                    processor.Emit(OpCodes.Stfld, applyVariable_handles);
                    processor.Emit(OpCodes.Ldloca, applyVariableJob);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldc_I4_1);
                    processor.Emit(OpCodes.Call, systemState_variableRequestHandle);
                    processor.Emit(OpCodes.Stfld, applyVariable_variables);
                    processor.Emit(OpCodes.Ldloc, applyVariableJob);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, entityQueryField);
                    processor.Emit(OpCodes.Ldloc, currentDependency);
                    processor.Emit(OpCodes.Call, applyVariableJob_schedule);
                    processor.Emit(OpCodes.Stloc, applyVariableJobHandle);
                    currentDependency = applyVariableJobHandle;
                }
                //Execute Action Job
                processor.Emit(OpCodes.Ldloca, executeActionJob);
                processor.Emit(OpCodes.Initobj, jobDefinition.definition);
                processor.Emit(OpCodes.Ldloca, executeActionJob);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldc_I4_1);
                processor.Emit(OpCodes.Call, systemState_executionRequestHandle);
                processor.Emit(OpCodes.Stfld, jobDefinition.requestHandleField);
                processor.Emit(OpCodes.Ldloca, executeActionJob);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetEntityTypeHandle))));
                processor.Emit(OpCodes.Stfld, jobDefinition.entityHandleField);
                processor.Emit(OpCodes.Ldloca, executeActionJob);
                processor.Emit(OpCodes.Ldloc, handles);
                processor.Emit(OpCodes.Stfld, jobDefinition.configHandlesField);
                processor.Emit(OpCodes.Ldloca, executeActionJob);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldc_I4_1);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetComponentDataFromEntity)).MakeGenericMethod(typeof(ActionExecutionRequestAt<TDelegate>))));
                processor.Emit(OpCodes.Stfld, jobDefinition.requestAtDataField);
                processor.Emit(OpCodes.Ldloca, executeActionJob);
                processor.Emit(OpCodes.Ldc_I4, (int)Allocator.TempJob);
                processor.Emit(OpCodes.Newobj, moduleDefinition.ImportReference(typeof(NativeQueue<int>).GetConstructor(new Type[] { typeof(Allocator) })));
                processor.Emit(OpCodes.Stfld, jobDefinition.nodeQueueField);
                processor.Emit(OpCodes.Ldloca, executeActionJob);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetSingleton)).MakeGenericMethod(typeof(ActionIndex<TDelegate>))));
                processor.Emit(OpCodes.Stfld, jobDefinition.actionIndexField);
                foreach (var actionParameter in jobDefinition.actionParameterFields.Where(p => p.componentType != null && p.fieldDefinition != null)) {
                    processor.Emit(OpCodes.Ldloca, executeActionJob);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldc_I4_1);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetComponentTypeHandle)).MakeGenericMethod(actionParameter.componentType)));
                    processor.Emit(OpCodes.Stfld, actionParameter.fieldDefinition);
                }
                if (jobDefinition.returnHandleField != null) {
                    processor.Emit(OpCodes.Ldloca, executeActionJob);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldc_I4_0);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetComponentTypeHandle)).MakeGenericMethod(typeof(ActionResult<,>).MakeGenericType(typeof(TDelegate), jobDefinition.returnType))));
                    processor.Emit(OpCodes.Stfld, jobDefinition.returnHandleField);
                }
                processor.Emit(OpCodes.Ldloc, executeActionJob);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, entityQueryField);
                processor.Emit(OpCodes.Ldloc, currentDependency);
                processor.Emit(OpCodes.Call, executeActionJob_schedule);
                processor.Emit(OpCodes.Stloc, executeActionJobHandle);
                processor.Emit(OpCodes.Ldloca, handles);
                processor.Emit(OpCodes.Ldloc, executeActionJobHandle);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(NativeArray<ConfigInfo>).GetMethod(nameof(NativeArray<ConfigInfo>.Dispose), new Type[] { typeof(JobHandle) })));
                processor.Emit(OpCodes.Ret);
                onUpdateMethod.Body.OptimizeMacros();
            }
            private void GenerateOnDestroy(ModuleDefinition moduleDefinition, ActionDefinitionAsset actionDefinitionAsset) {
                onDestroyMethod = new MethodDefinition("OnDestroy", Mono.Cecil.MethodAttributes.Family | Mono.Cecil.MethodAttributes.CheckAccessOnOverride, moduleDefinition.TypeSystem.Void)
                {
                    IsVirtual = true,
                    IsReuseSlot = true,
                    IsHideBySig = true
                };
                definition.Methods.Add(onDestroyMethod);
                var processor = onDestroyMethod.Body.GetILProcessor();
                processor.Emit(OpCodes.Nop);
                processor.Emit(OpCodes.Ret);
            }
            private void GenerateJob(ModuleDefinition moduleDefinition, ActionDefinitionAsset actionDefinitionAsset) {
                jobDefinition = new ActionExecutionJobDefinition(moduleDefinition, definition, actionDefinitionAsset);
                definition.NestedTypes.Add(jobDefinition.definition);
            }
            public class ActionExecutionJobDefinition {
                public struct ParameterInfo {
                    public FieldDefinition fieldDefinition;
                    public Type componentType;
                    public bool singleton;
                    public string fieldName;
                    public VariableDefinition variableSource;
                    public Type parameterType;
                    public VariableDefinition variableCurrent;
                }
                public Type returnType;
                public Type returnTypeComponent;
                public TypeReference returnTypeComponentTypeHandleReference;
                TypeReference returnTypeComponentReference;
                public bool HasReturnType { get => returnType != typeof(void); }
                public MethodReference returnTypeAggregator;
                public bool HasReturnTypeAggregator { get => returnTypeAggregator != null; }
                public TypeDefinition definition;
                public FieldDefinition requestHandleField;
                public FieldDefinition entityHandleField;
                public FieldDefinition requestAtDataField;
                public FieldDefinition configHandlesField;
                public FieldDefinition actionIndexField;
                public FieldDefinition nodeQueueField;
                public MethodDefinition executeMethod;
                public List<ParameterInfo> actionParameterFields;
                public FieldDefinition returnHandleField;
                public ActionExecutionJobDefinition(ModuleDefinition moduleDefinition, TypeDefinition containerTypeDefinition, ActionDefinitionAsset actionDefinitionAsset) {
                    this.definition = new TypeDefinition(string.Empty, "ActionExecutionJob", Mono.Cecil.TypeAttributes.NestedPublic | Mono.Cecil.TypeAttributes.SequentialLayout | Mono.Cecil.TypeAttributes.Sealed, moduleDefinition.ImportReference(typeof(ValueType)));
                    definition.Interfaces.Add(new InterfaceImplementation(moduleDefinition.ImportReference(typeof(IJobEntityBatch))));
                    definition.CustomAttributes.Add(new CustomAttribute(moduleDefinition.ImportReference(typeof(BurstCompileAttribute).GetConstructor(Type.EmptyTypes))));
                    var readOnlyAttributeReference = moduleDefinition.ImportReference(typeof(ReadOnlyAttribute).GetConstructor(Type.EmptyTypes));
                    this.returnType = typeof(TDelegate).GetMethod("Invoke").ReturnType;
                    this.returnTypeAggregator = actionDefinitionAsset.aggregator.IsCreated ? moduleDefinition.ImportReference(actionDefinitionAsset.aggregator.Value) : null;
                    requestHandleField = new FieldDefinition("requestHandle", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(typeof(ComponentTypeHandle<>).MakeGenericType(typeof(ActionExecutionRequest<TDelegate>))));
                    requestHandleField.CustomAttributes.Add(new CustomAttribute(readOnlyAttributeReference));
                    definition.Fields.Add(requestHandleField);
                    entityHandleField = new FieldDefinition("entityHandle", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(typeof(EntityTypeHandle)));
                    entityHandleField.CustomAttributes.Add(new CustomAttribute(readOnlyAttributeReference));
                    definition.Fields.Add(entityHandleField);
                    requestAtDataField = new FieldDefinition("requestAtData", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(typeof(ComponentDataFromEntity<>).MakeGenericType(typeof(ActionExecutionRequestAt<TDelegate>))));
                    requestAtDataField.CustomAttributes.Add(new CustomAttribute(readOnlyAttributeReference));
                    definition.Fields.Add(requestAtDataField);
                    configHandlesField = new FieldDefinition("configHandles", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(typeof(NativeArray<ConfigInfo>)));
                    configHandlesField.CustomAttributes.Add(new CustomAttribute(readOnlyAttributeReference));
                    definition.Fields.Add(configHandlesField);
                    actionIndexField = new FieldDefinition("index", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(typeof(ActionIndex<TDelegate>)));
                    actionIndexField.CustomAttributes.Add(new CustomAttribute(readOnlyAttributeReference));
                    definition.Fields.Add(actionIndexField);
                    nodeQueueField = new FieldDefinition("nodeQueue", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(typeof(NativeQueue<int>)));
                    nodeQueueField.CustomAttributes.Add(new CustomAttribute(moduleDefinition.ImportReference(typeof(DeallocateOnJobCompletionAttribute).GetConstructor(Type.EmptyTypes))));
                    definition.Fields.Add(nodeQueueField);
                    if (HasReturnType && HasReturnTypeAggregator) {
                        returnTypeComponent = typeof(ActionResult<,>).MakeGenericType(typeof(TDelegate), returnType);
                        returnTypeComponentReference = moduleDefinition.ImportReference(returnTypeComponent);
                        returnTypeComponentTypeHandleReference = moduleDefinition.ImportReference(typeof(ComponentTypeHandle<>).MakeGenericType(returnTypeComponent));
                        returnHandleField = new FieldDefinition("returnHandle", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(returnTypeComponentTypeHandleReference));
                        definition.Fields.Add(returnHandleField);
                    }
                    var components = actionDefinitionAsset.GetComponents();
                    actionParameterFields = actionDefinitionAsset.delegateType.Value.GetMethod("Invoke").GetParameters().Select(parameter =>
                    {
                        if (parameter.ParameterType == typeof(ConfigDataLength)) {
                            return new ParameterInfo
                            {
                                variableCurrent = new VariableDefinition(moduleDefinition.ImportReference(typeof(ConfigDataLength))),
                                parameterType = typeof(ConfigDataLength)
                            };
                        }
                        else if (parameter.ParameterType == typeof(ConfigDataHandle)) {
                            return new ParameterInfo
                            {
                                variableCurrent = new VariableDefinition(moduleDefinition.ImportReference(typeof(ConfigDataHandle))),
                                parameterType = typeof(ConfigDataHandle)
                            };
                        }
                        else if (components.TryGetValue(parameter.Name, out var component)) {
                            var info = new ParameterInfo
                            {
                                fieldDefinition = new FieldDefinition($"{parameter.Name}_parameterHandle", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(typeof(ComponentTypeHandle<>).MakeGenericType(component.componentType))),
                                parameterType = parameter.ParameterType,
                                fieldName = component.fieldName,
                                singleton = component.singletonTarget,
                                componentType = component.componentType,
                                variableCurrent = new VariableDefinition(moduleDefinition.ImportReference(parameter.ParameterType)),
                                variableSource = new VariableDefinition(moduleDefinition.ImportReference(typeof(NativeArray<>).MakeGenericType(component.componentType)))
                            };
                            info.fieldDefinition.CustomAttributes.Add(new CustomAttribute(readOnlyAttributeReference));
                            definition.Fields.Add(info.fieldDefinition);
                            return info;
                        }
                        else {
                            return new ParameterInfo
                            {
                                parameterType = parameter.ParameterType,
                                singleton = component.singletonTarget,
                                variableCurrent = new VariableDefinition(moduleDefinition.ImportReference(parameter.ParameterType))
                            };
                        }
                    }).ToList();
                    GenerateExecute(moduleDefinition, actionDefinitionAsset);
                }
                private void GenerateExecute(ModuleDefinition moduleDefinition, ActionDefinitionAsset actionDefinitionAsset) {
                    //Type References
                    var nativeArray_entity = moduleDefinition.ImportReference(typeof(NativeArray<Entity>));
                    var nativeArray_actionExecutionRequest = moduleDefinition.ImportReference(typeof(NativeArray<ActionExecutionRequest<TDelegate>>));
                    var blobAssetReference_blobGraph = moduleDefinition.ImportReference(typeof(BlobAssetReference<BlobGraph<TDelegate>>));
                    var entityTypeHandle = moduleDefinition.ImportReference(typeof(EntityTypeHandle));
                    var componentTypeHandle_actionExecutionRequest = moduleDefinition.ImportReference(typeof(ComponentTypeHandle<ActionExecutionRequest<TDelegate>>));
                    var componentDataFromEntity_actionExecutionRequestAt = moduleDefinition.ImportReference(typeof(ComponentDataFromEntity<ActionExecutionRequestAt<TDelegate>>));
                    var blobGraphNode = moduleDefinition.ImportReference(typeof(BlobGraphNode));
                    var functionPointer = moduleDefinition.ImportReference(typeof(FunctionPointer<TDelegate>));
                    var nativeQueue_int = moduleDefinition.ImportReference(typeof(NativeQueue<int>));
                    var archetypeChunk = moduleDefinition.ImportReference(typeof(ArchetypeChunk));
                    var blobArray_blobGraphNode = moduleDefinition.ImportReference(typeof(BlobArray<BlobGraphNode>));
                    var configInfo = moduleDefinition.ImportReference(typeof(ConfigInfo));
                    var configHandle = moduleDefinition.ImportReference(typeof(ConfigDataHandle));
                    //Method References
                    var nativeArray_actionExecutionRequest_length = moduleDefinition.ImportReference(typeof(NativeArray<ActionExecutionRequest<TDelegate>>).GetProperty(nameof(NativeArray<ActionExecutionRequest<TDelegate>>.Length)).GetMethod);
                    var nativeArray_actionExecutionRequest_item = moduleDefinition.ImportReference(typeof(NativeArray<ActionExecutionRequest<TDelegate>>).GetProperty("Item").GetMethod);
                    var nativeArray_configInfo_item = moduleDefinition.ImportReference(typeof(NativeArray<ConfigInfo>).GetProperty("Item").GetMethod);
                    var blobAssetReference_blobGraph_value = moduleDefinition.ImportReference(typeof(BlobAssetReference<BlobGraph<TDelegate>>).GetProperty(nameof(BlobAssetReference<BlobGraph<TDelegate>>.Value)).GetMethod);
                    var blobAssetReference_blobGraph_isCreated = moduleDefinition.ImportReference(typeof(BlobAssetReference<BlobGraph<TDelegate>>).GetProperty(nameof(BlobAssetReference<BlobGraph<TDelegate>>.IsCreated)).GetMethod);
                    var nativeQueue_int_enqueue = moduleDefinition.ImportReference(typeof(NativeQueue<int>).GetMethod(nameof(NativeQueue<int>.Enqueue)));
                    var nativeQueue_int_dequeue = moduleDefinition.ImportReference(typeof(NativeQueue<int>).GetMethod(nameof(NativeQueue<int>.Dequeue)));
                    var nativeQueue_int_clear = moduleDefinition.ImportReference(typeof(NativeQueue<int>).GetMethod(nameof(NativeQueue<int>.Clear)));
                    var nativeQueue_int_isEmpty = moduleDefinition.ImportReference(typeof(NativeQueue<int>).GetMethod(nameof(NativeQueue<int>.IsEmpty)));
                    var componentDataFromEntity_actionExecutionRequestAt_hasComponent = moduleDefinition.ImportReference(typeof(ComponentDataFromEntity<ActionExecutionRequestAt<TDelegate>>).GetMethod(nameof(ComponentDataFromEntity<ActionExecutionRequestAt<TDelegate>>.HasComponent)));
                    var componentDataFromEntity_actionExecutionRequestAt_item = moduleDefinition.ImportReference(typeof(ComponentDataFromEntity<ActionExecutionRequestAt<TDelegate>>).GetProperty("Item").GetMethod);
                    var nativeArray_entity_item = moduleDefinition.ImportReference(typeof(NativeArray<Entity>).GetProperty("Item").GetMethod);
                    var actionIndex_item = moduleDefinition.ImportReference(typeof(ActionIndex<TDelegate>).GetProperty("Item").GetMethod);
                    var archetypeChunk_getNativeArray_entityTypeHandle = moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetMethod(nameof(ArchetypeChunk.GetNativeArray), new Type[] { typeof(EntityTypeHandle) }));
                    var archetypeChunk_getNativeArray = moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetMethods(BindingFlags.Public | BindingFlags.Instance).First(m => m.Name == nameof(ArchetypeChunk.GetNativeArray) && m.IsGenericMethod).MakeGenericMethod(typeof(ActionExecutionRequest<TDelegate>)));
                    MethodReference archetypeChunk_getNativeArray_returnTypeHandle = null;
                    MethodReference nativeArray_return_item_set = null;
                    if (HasReturnType && HasReturnTypeAggregator) {
                        var resultType = typeof(ActionResult<,>).MakeGenericType(typeof(TDelegate), returnType);
                        archetypeChunk_getNativeArray_returnTypeHandle = moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetMethods(BindingFlags.Public | BindingFlags.Instance).First(m => m.Name == nameof(ArchetypeChunk.GetNativeArray) && m.IsGenericMethod).MakeGenericMethod(resultType));
                        nativeArray_return_item_set = moduleDefinition.ImportReference(typeof(NativeArray<>).MakeGenericType(resultType).GetProperty("Item").SetMethod);
                    }
                    var blobArray_int_length = moduleDefinition.ImportReference(typeof(BlobArray<int>).GetProperty(nameof(BlobArray<int>.Length)).GetMethod);
                    var blobArray_int_item = moduleDefinition.ImportReference(typeof(BlobArray<int>).GetProperty("Item").GetMethod);
                    var blobArray_blobGraphNode_item = moduleDefinition.ImportReference(typeof(BlobArray<BlobGraphNode>).GetProperty("Item").GetMethod);
                    var functionPointer_invoke = moduleDefinition.ImportReference(typeof(FunctionPointer<TDelegate>).GetProperty("Invoke").GetMethod);
                    //Fields
                    var actionExecutionRequestAt_startIndex = moduleDefinition.ImportReference(typeof(ActionExecutionRequestAt<TDelegate>).GetField(nameof(ActionExecutionRequestAt<TDelegate>.startIndex)));
                    var blobGraphNode_id = moduleDefinition.ImportReference(typeof(BlobGraphNode).GetField(nameof(BlobGraphNode.id)));
                    var blobGraphNode_next = moduleDefinition.ImportReference(typeof(BlobGraphNode).GetField(nameof(BlobGraphNode.next)));
                    var blobGraph_roots = moduleDefinition.ImportReference(typeof(BlobGraph<TDelegate>).GetField(nameof(BlobGraph<TDelegate>.roots)));
                    var blobGraph_nodes = moduleDefinition.ImportReference(typeof(BlobGraph<TDelegate>).GetField(nameof(BlobGraph<TDelegate>.nodes)));
                    var actionExecutionRequest_value = moduleDefinition.ImportReference(typeof(ActionExecutionRequest<TDelegate>).GetField(nameof(ActionExecutionRequest<TDelegate>.value)));
                    var configHandle_handle = moduleDefinition.ImportReference(typeof(ConfigInfo).GetField(nameof(ConfigInfo.handle)));
                    var configHandle_length = moduleDefinition.ImportReference(typeof(ConfigInfo).GetField(nameof(ConfigInfo.length)));
                    FieldReference return_value = null;
                    if (HasReturnType && HasReturnTypeAggregator) {
                        return_value = moduleDefinition.ImportReference(returnTypeComponent.GetField("value"));
                    }
                    //Method Definition
                    executeMethod = new MethodDefinition(nameof(IJobEntityBatch.Execute), Mono.Cecil.MethodAttributes.Public, moduleDefinition.TypeSystem.Void)
                    {
                        IsVirtual = true,
                        IsReuseSlot = true,
                        IsHideBySig = true
                    };
                    //executeMethod.CustomAttributes.Add(new CustomAttribute(moduleDefinition.ImportReference(typeof(BurstCompileAttribute).GetConstructor(Type.EmptyTypes))));
                    //Parameters
                    var batchInChunk = new ParameterDefinition(archetypeChunk);
                    var batchIndex = new ParameterDefinition(moduleDefinition.TypeSystem.Int32);
                    executeMethod.Parameters.Add(batchInChunk);
                    executeMethod.Parameters.Add(batchIndex);
                    definition.Methods.Add(executeMethod);
                    executeMethod.Body.InitLocals = true;
                    executeMethod.Body.SimplifyMacros();
                    var processor = executeMethod.Body.GetILProcessor();
                    //Variables
                    var var0 = new VariableDefinition(nativeArray_actionExecutionRequest);
                    var var1 = new VariableDefinition(nativeArray_entity);
                    var var2 = new VariableDefinition(moduleDefinition.TypeSystem.Int32);
                    var var3 = new VariableDefinition(blobAssetReference_blobGraph);
                    var var4 = new VariableDefinition(moduleDefinition.TypeSystem.Boolean);
                    var var5 = new VariableDefinition(moduleDefinition.TypeSystem.Boolean);
                    var var6 = new VariableDefinition(moduleDefinition.TypeSystem.Int32);
                    var var7 = new VariableDefinition(moduleDefinition.TypeSystem.Boolean);
                    var var8 = new VariableDefinition(blobGraphNode);
                    var var9 = new VariableDefinition(functionPointer);
                    var var10 = new VariableDefinition(moduleDefinition.TypeSystem.Boolean);
                    var var11 = new VariableDefinition(moduleDefinition.TypeSystem.Boolean);
                    var var12 = new VariableDefinition(moduleDefinition.TypeSystem.Boolean);
                    var var13 = new VariableDefinition(moduleDefinition.ImportReference(typeof(FunctionPointer<TDelegate>)));
                    var var14 = new VariableDefinition(moduleDefinition.ImportReference(typeof(ConfigInfo)));
                    var var15 = new VariableDefinition(moduleDefinition.TypeSystem.Boolean);
                    VariableDefinition returnVar1 = null;
                    VariableDefinition returnVar2 = null;
                    VariableDefinition returnVarSource = null;
                    VariableDefinition returnVarComponent = null;
                    if (HasReturnType && HasReturnTypeAggregator) {
                        returnVar1 = new VariableDefinition(moduleDefinition.ImportReference(returnType));
                        returnVar2 = new VariableDefinition(moduleDefinition.ImportReference(returnType));
                        returnVarComponent = new VariableDefinition(moduleDefinition.ImportReference(returnTypeComponent));
                        returnVarSource = new VariableDefinition(moduleDefinition.ImportReference(typeof(NativeArray<>).MakeGenericType(typeof(ActionResult<,>).MakeGenericType(typeof(TDelegate), returnType))));
                    }
                    executeMethod.Body.Variables.Add(var0);
                    executeMethod.Body.Variables.Add(var1);
                    executeMethod.Body.Variables.Add(var2);
                    executeMethod.Body.Variables.Add(var3);
                    executeMethod.Body.Variables.Add(var4);
                    executeMethod.Body.Variables.Add(var5);
                    executeMethod.Body.Variables.Add(var6);
                    executeMethod.Body.Variables.Add(var7);
                    executeMethod.Body.Variables.Add(var8);
                    executeMethod.Body.Variables.Add(var9);
                    executeMethod.Body.Variables.Add(var10);
                    executeMethod.Body.Variables.Add(var11);
                    executeMethod.Body.Variables.Add(var12);
                    executeMethod.Body.Variables.Add(var13);
                    executeMethod.Body.Variables.Add(var14);
                    executeMethod.Body.Variables.Add(var15);

                    foreach (var parameter in actionParameterFields) {
                        if (parameter.variableSource != null) {
                            executeMethod.Body.Variables.Add(parameter.variableSource);
                        }
                        executeMethod.Body.Variables.Add(parameter.variableCurrent);
                    }

                    if (HasReturnType && HasReturnTypeAggregator) {
                        executeMethod.Body.Variables.Add(returnVar1);
                        executeMethod.Body.Variables.Add(returnVar2);
                        executeMethod.Body.Variables.Add(returnVarComponent);
                        executeMethod.Body.Variables.Add(returnVarSource);
                    }


                    //Branches
                    var bl_0158 = processor.Create(OpCodes.Ldloc_2);
                    var bl_0153 = processor.Create(OpCodes.Nop);
                    var bl_0085 = processor.Create(OpCodes.Nop);
                    var bl_0131 = processor.Create(OpCodes.Nop);
                    var bl_0132 = processor.Create(OpCodes.Ldarg_0);
                    var bl_00cf = processor.Create(OpCodes.Br, bl_0132);
                    var bl_00b3 = processor.Create(OpCodes.Ldloc_S, var6);
                    var bl_008b = processor.Create(OpCodes.Nop);
                    var bl_00d1 = processor.Create(OpCodes.Nop);

                    var bl_0024 = processor.Create(OpCodes.Nop);

                    processor.Emit(OpCodes.Nop);
                    // NativeArray<ActionExecutionRequest<SampleDelegate>> nativeArray = P_0.GetNativeArray(requestHandle);
                    processor.Emit(OpCodes.Ldarga_S, batchInChunk);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, requestHandleField);
                    processor.Emit(OpCodes.Call, archetypeChunk_getNativeArray);
                    processor.Emit(OpCodes.Stloc_0);
                    // NativeArray<Entity> nativeArray2 = P_0.GetNativeArray(entityHandle);
                    processor.Emit(OpCodes.Ldarga_S, batchInChunk);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, entityHandleField);
                    processor.Emit(OpCodes.Call, archetypeChunk_getNativeArray_entityTypeHandle);
                    processor.Emit(OpCodes.Stloc_1);
                    if (HasReturnType && HasReturnTypeAggregator) {
                        processor.Emit(OpCodes.Ldarga_S, batchInChunk);
                        processor.Emit(OpCodes.Ldarg_0);
                        processor.Emit(OpCodes.Ldfld, returnHandleField);
                        processor.Emit(OpCodes.Call, archetypeChunk_getNativeArray_returnTypeHandle);
                        processor.Emit(OpCodes.Stloc_S, returnVarSource);
                    }
                    //Parameter Native Arrays
                    var parameterNativeArrayGetter = typeof(ArchetypeChunk).GetGenericMethod(nameof(ArchetypeChunk.GetNativeArray), BindingFlags.Public | BindingFlags.Instance);
                    foreach (var parameterInfo in actionParameterFields.Where(p => p.fieldDefinition != null)) {
                        processor.Emit(OpCodes.Ldarga_S, batchInChunk);
                        processor.Emit(OpCodes.Ldarg_0);
                        processor.Emit(OpCodes.Ldfld, parameterInfo.fieldDefinition);
                        processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(parameterNativeArrayGetter.MakeGenericMethod(parameterInfo.componentType)));
                        processor.Emit(OpCodes.Stloc_S, parameterInfo.variableSource);
                    }
                    // for (int i = 0; i < nativeArray.Length; i++)
                    processor.Emit(OpCodes.Ldc_I4_0);
                    processor.Emit(OpCodes.Stloc_2);
                    processor.Emit(OpCodes.Br, bl_0158);
                    // BlobAssetReference<BlobGraph<SampleDelegate>> value = nativeArray[i].value;
                    processor.Append(bl_0024);
                    processor.Emit(OpCodes.Ldloca_S, var0);
                    processor.Emit(OpCodes.Ldloc_2);
                    processor.Emit(OpCodes.Call, nativeArray_actionExecutionRequest_item);
                    processor.Emit(OpCodes.Ldfld, actionExecutionRequest_value);
                    processor.Emit(OpCodes.Stloc_3);
                    //Set flag
                    processor.Emit(OpCodes.Ldc_I4_1);
                    processor.Emit(OpCodes.Stloc, var15);
                    //Get Parameters
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, configHandlesField);
                    processor.Emit(OpCodes.Ldloc_2);
                    processor.Emit(OpCodes.Call, nativeArray_configInfo_item);
                    processor.Emit(OpCodes.Stloc_S, var14);
                    foreach (var parameterInfo in actionParameterFields) {
                        if (parameterInfo.parameterType == typeof(ConfigDataHandle)) {
                            processor.Emit(OpCodes.Ldloca_S, var14);
                            processor.Emit(OpCodes.Ldfld, configHandle_handle);
                            processor.Emit(OpCodes.Stloc_S, parameterInfo.variableCurrent);
                        }
                        else if (parameterInfo.parameterType == typeof(ConfigDataLength)) {
                            processor.Emit(OpCodes.Ldloca_S, var14);
                            processor.Emit(OpCodes.Ldfld, configHandle_length);
                            processor.Emit(OpCodes.Stloc_S, parameterInfo.variableCurrent);
                        }
                        else if (parameterInfo.variableSource != null) {
                            processor.Emit(OpCodes.Ldloca_S, parameterInfo.variableSource);
                            processor.Emit(OpCodes.Ldloc_2);
                            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(NativeArray<>).MakeGenericType(parameterInfo.componentType).GetProperty("Item").GetMethod));
                            processor.Emit(OpCodes.Ldfld, moduleDefinition.ImportReference(parameterInfo.componentType.GetField(parameterInfo.fieldName)));
                            processor.Emit(OpCodes.Stloc_S, parameterInfo.variableCurrent);
                        }
                    }


                    // if (!value.IsCreated)
                    processor.Emit(OpCodes.Ldloca_S, var3);
                    processor.Emit(OpCodes.Call, blobAssetReference_blobGraph_isCreated);
                    processor.Emit(OpCodes.Stloc_S, var4);
                    processor.Emit(OpCodes.Ldloc_S, var4);
                    processor.Emit(OpCodes.Brfalse, bl_0153);

                    // if (requestAtData.HasComponent(nativeArray2[i]))
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, requestAtDataField);
                    processor.Emit(OpCodes.Ldloca_S, var1);
                    processor.Emit(OpCodes.Ldloc_2);
                    processor.Emit(OpCodes.Call, nativeArray_entity_item);
                    processor.Emit(OpCodes.Call, componentDataFromEntity_actionExecutionRequestAt_hasComponent);
                    processor.Emit(OpCodes.Stloc_S, var5);
                    processor.Emit(OpCodes.Ldloc_S, var5);
                    processor.Emit(OpCodes.Brfalse, bl_0085);
                    // nodeQueue.Enqueue(requestAtData[nativeArray2[i]].startIndex);
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, nodeQueueField);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, requestAtDataField);
                    processor.Emit(OpCodes.Ldloca_S, var1);
                    processor.Emit(OpCodes.Ldloc_2);
                    processor.Emit(OpCodes.Call, nativeArray_entity_item);
                    processor.Emit(OpCodes.Call, componentDataFromEntity_actionExecutionRequestAt_item);
                    processor.Emit(OpCodes.Ldfld, actionExecutionRequestAt_startIndex);
                    processor.Emit(OpCodes.Call, nativeQueue_int_enqueue);
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Br, bl_00cf);
                    // for (int j = 0; j < value.Value.roots.Length; j++)
                    processor.Append(bl_0085);
                    processor.Emit(OpCodes.Ldc_I4_0);
                    processor.Emit(OpCodes.Stloc_S, var6);
                    processor.Emit(OpCodes.Br, bl_00b3);
                    // nodeQueue.Enqueue(value.Value.roots[j]);
                    processor.Append(bl_008b);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, nodeQueueField);
                    processor.Emit(OpCodes.Ldloca_S, var3);
                    processor.Emit(OpCodes.Call, blobAssetReference_blobGraph_value);
                    processor.Emit(OpCodes.Ldflda, blobGraph_roots);
                    processor.Emit(OpCodes.Ldloc_S, var6);
                    processor.Emit(OpCodes.Call, blobArray_int_item);
                    processor.Emit(OpCodes.Ldind_I4);
                    processor.Emit(OpCodes.Call, nativeQueue_int_enqueue);
                    processor.Emit(OpCodes.Nop);
                    // for (int j = 0; j < value.Value.roots.Length; j++)
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldloc_S, var6);
                    processor.Emit(OpCodes.Ldc_I4_1);
                    processor.Emit(OpCodes.Add); //IL_00B0
                    processor.Emit(OpCodes.Stloc_S, var6);
                    // for (int j = 0; j < value.Value.roots.Length; j++)
                    processor.Append(bl_00b3);
                    processor.Emit(OpCodes.Ldloca_S, var3);
                    processor.Emit(OpCodes.Call, blobAssetReference_blobGraph_value);
                    processor.Emit(OpCodes.Ldflda, blobGraph_roots);
                    processor.Emit(OpCodes.Call, blobArray_int_length);
                    processor.Emit(OpCodes.Clt);
                    processor.Emit(OpCodes.Stloc_S, var7);
                    processor.Emit(OpCodes.Ldloc_S, var7);
                    processor.Emit(OpCodes.Brtrue, bl_008b);
                    processor.Emit(OpCodes.Nop);
                    processor.Append(bl_00cf);


                    processor.Append(bl_00d1);

                    processor.Emit(OpCodes.Ldloca_S, var3);
                    processor.Emit(OpCodes.Call, blobAssetReference_blobGraph_value);
                    processor.Emit(OpCodes.Ldflda, blobGraph_nodes);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, nodeQueueField);
                    processor.Emit(OpCodes.Call, nativeQueue_int_dequeue);
                    processor.Emit(OpCodes.Call, blobArray_blobGraphNode_item);
                    processor.Emit(OpCodes.Ldobj, blobGraphNode);
                    processor.Emit(OpCodes.Stloc_S, var8);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, actionIndexField);
                    processor.Emit(OpCodes.Ldloc_S, var8);
                    processor.Emit(OpCodes.Ldfld, blobGraphNode_id);
                    processor.Emit(OpCodes.Call, actionIndex_item);
                    processor.Emit(OpCodes.Stloc_S, var13);
                    //Call
                    if (HasReturnType && HasReturnTypeAggregator) {
                        //processor.Emit(OpCodes.Ldloc_S, returnVar);
                    }
                    processor.Emit(OpCodes.Ldloca_S, var13);
                    processor.Emit(OpCodes.Call, functionPointer_invoke);
                    foreach (var item in actionParameterFields) {
                        processor.Emit(OpCodes.Ldloc_S, item.variableCurrent);
                    }
                    processor.Emit(OpCodes.Callvirt, moduleDefinition.ImportReference(typeof(TDelegate).GetMethod("Invoke")));
                    processor.Emit(OpCodes.Stloc_S, returnVar2);
                    if (HasReturnType) {
                        if (HasReturnTypeAggregator) {
                            var br1 = processor.Create(OpCodes.Nop);
                            var br2 = processor.Create(OpCodes.Nop);
                            processor.Emit(OpCodes.Ldloc_S, var15);
                            processor.Emit(OpCodes.Brfalse, br1);
                            processor.Emit(OpCodes.Ldloc_S, returnVar2);
                            processor.Emit(OpCodes.Stloc, returnVar1);
                            processor.Emit(OpCodes.Ldc_I4_0);
                            processor.Emit(OpCodes.Stloc, var15);
                            processor.Emit(OpCodes.Br, br2);
                            processor.Append(br1);
                            processor.Emit(OpCodes.Ldloc_S, returnVar1);
                            processor.Emit(OpCodes.Ldloc_S, returnVar2);
                            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(returnTypeAggregator));
                            processor.Emit(OpCodes.Stloc_S, returnVar1);
                            processor.Append(br2);

                        }
                        else {
                            processor.Emit(OpCodes.Pop);
                        }
                    }
                    processor.Emit(OpCodes.Nop);

                    //Inject return update
                    processor.Emit(OpCodes.Ldloc_S, var8);
                    processor.Emit(OpCodes.Ldfld, blobGraphNode_next);
                    processor.Emit(OpCodes.Ldc_I4_0);
                    processor.Emit(OpCodes.Clt);
                    processor.Emit(OpCodes.Ldc_I4_0);
                    processor.Emit(OpCodes.Ceq);
                    processor.Emit(OpCodes.Stloc_S, var10);
                    processor.Emit(OpCodes.Ldloc_S, var10);
                    processor.Emit(OpCodes.Brfalse, bl_0131);
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, nodeQueueField);
                    processor.Emit(OpCodes.Ldloc_S, var8);
                    processor.Emit(OpCodes.Ldfld, blobGraphNode_next);
                    processor.Emit(OpCodes.Call, nativeQueue_int_enqueue);
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Nop);
                    processor.Append(bl_0131);
                    processor.Append(bl_0132);
                    processor.Emit(OpCodes.Ldflda, nodeQueueField);
                    processor.Emit(OpCodes.Call, nativeQueue_int_isEmpty);
                    processor.Emit(OpCodes.Ldc_I4_0);
                    processor.Emit(OpCodes.Ceq);
                    processor.Emit(OpCodes.Stloc_S, var11);
                    processor.Emit(OpCodes.Ldloc_S, var11);
                    processor.Emit(OpCodes.Brtrue, bl_00d1);
                    //Update result
                    if (HasReturnType && HasReturnTypeAggregator) {
                        processor.Emit(OpCodes.Ldloca_S, returnVarSource);
                        processor.Emit(OpCodes.Ldloc_2);
                        processor.Emit(OpCodes.Ldloca_S, returnVarComponent);
                        processor.Emit(OpCodes.Initobj, returnTypeComponentReference);
                        processor.Emit(OpCodes.Ldloca_S, returnVarComponent);
                        processor.Emit(OpCodes.Ldloc_S, returnVar1);
                        processor.Emit(OpCodes.Stfld, return_value);
                        processor.Emit(OpCodes.Ldloc_S, returnVarComponent);
                        processor.Emit(OpCodes.Call, nativeArray_return_item_set);
                    }
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Nop);
                    processor.Append(bl_0153);
                    processor.Emit(OpCodes.Ldloc_2);
                    processor.Emit(OpCodes.Ldc_I4_1);
                    processor.Emit(OpCodes.Add);
                    processor.Emit(OpCodes.Stloc_2);
                    processor.Append(bl_0158);
                    processor.Emit(OpCodes.Ldloca_S, var0);
                    processor.Emit(OpCodes.Call, nativeArray_actionExecutionRequest_length);
                    processor.Emit(OpCodes.Clt);
                    processor.Emit(OpCodes.Stloc_S, var12);
                    processor.Emit(OpCodes.Ldloc_S, var12);
                    processor.Emit(OpCodes.Brtrue, bl_0024);
                    processor.Emit(OpCodes.Ret);
                    executeMethod.Body.OptimizeMacros();
                }
            }
        }
    }
}