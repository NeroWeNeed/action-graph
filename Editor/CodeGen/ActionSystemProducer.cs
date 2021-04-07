using System;
using System.Collections.Generic;
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
using UnityEditor.Compilation;
using UnityEngine;

namespace NeroWeNeed.ActionGraph.Editor.CodeGen {

    public static class ActionSystemProducer {
        public const string GeneratedNamespaceExtension = "Generated";
        public const string ActionExecutionSystemBaseName = "ActionExecutionSystem";
        public const string AssemblyName = "NeroWeNeed.ActionGraph.ActionSystems";
        [MenuItem("Assets/Generate Action Assembly")]
        public static void CreateAssembly() {
            var settings = ProjectUtility.GetOrCreateProjectSettings<ActionGraphGlobalSettings>();
            var output = settings.actionSystemsDLLDirectory + "/" + AssemblyName + ".dll";
            var definitions = ActionDefinitionAsset.LoadAll();
            using var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory("Library/ScriptAssemblies");
            using (var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(AssemblyName, new Version(1, 0, 0, 0)), AssemblyName, new ModuleParameters { Kind = ModuleKind.Dll, AssemblyResolver = resolver, Runtime = TargetRuntime.Net_4_0, })) {
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
                assembly.Write(output);
            }
            AssetDatabase.ImportAsset(output);
        }
        private static string GetNamespace(string baseNamespace) => string.IsNullOrEmpty(baseNamespace) ? GeneratedNamespaceExtension : $"{baseNamespace}.{GeneratedNamespaceExtension}";
        private abstract class ActionExecutionSystemDefinition {
            public TypeDefinition definition;
            public FieldDefinition entityQueryField;
            public FieldDefinition entityCommandBufferSystemField;
            public MethodDefinition onCreateMethod;
            public MethodDefinition onUpdateMethod;
            public MethodDefinition onDestroyMethod;
            public Type variableType;
            public TypeReference variableTypeReference;
            public Type entityCommandBufferSystemType;
            public TypeReference entityCommandBufferSystemTypeReference;
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
        private class ActionExecutionSystemDefinition<TAction> : ActionExecutionSystemDefinition where TAction : Delegate {
            public ActionExecutionJobDefinition jobDefinition;
            public ActionExecutionSystemDefinition(ModuleDefinition moduleDefinition, ActionDefinitionAsset actionDefinitionAsset) {
                definition = new TypeDefinition(GetNamespace(typeof(TAction).Namespace), $"{ActionExecutionSystemBaseName}_{typeof(TAction).FullName.Replace('.', '_')}", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Sealed, moduleDefinition.ImportReference(typeof(SystemBase)));
                entityQueryField = new FieldDefinition("query", Mono.Cecil.FieldAttributes.Private, moduleDefinition.ImportReference(typeof(EntityQuery)));
                definition.Fields.Add(entityQueryField);
                if (actionDefinitionAsset.destroyEntitiesUsing.IsCreated) {
                    entityCommandBufferSystemType = actionDefinitionAsset.destroyEntitiesUsing;
                    entityCommandBufferSystemTypeReference = moduleDefinition.ImportReference(actionDefinitionAsset.destroyEntitiesUsing);
                    entityCommandBufferSystemField = new FieldDefinition("entityCommandBufferSystem", Mono.Cecil.FieldAttributes.Private, entityCommandBufferSystemTypeReference);

                    definition.Fields.Add(entityCommandBufferSystemField);
                }

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
                generics.Add(GenericAttributeInfo.Component(moduleDefinition, typeof(ActionRequest<TAction>)));
                generics.Add(GenericAttributeInfo.Component(moduleDefinition, typeof(ActionRequestAt<TAction>)));
                generics.Add(GenericAttributeInfo.Component(moduleDefinition, typeof(Action<TAction>)));

                generics.Add(GenericAttributeInfo.Job(moduleDefinition, typeof(ActionExecutionConfigInitJob<TAction>)));
                if (HasVariable) {
                    generics.Add(GenericAttributeInfo.Job(moduleDefinition, typeof(ActionExecutionApplyVariableJob<,>).MakeGenericType(typeof(TAction), variableType)));
                    generics.Add(GenericAttributeInfo.Component(moduleDefinition, typeof(ActionVariable<,>).MakeGenericType(typeof(TAction), variableType)));
                }
                if (jobDefinition.HasReturnType && jobDefinition.HasReturnTypeAggregator) {
                    generics.Add(GenericAttributeInfo.Component(moduleDefinition, typeof(ActionResult<,>).MakeGenericType(typeof(TAction), jobDefinition.returnType)));
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
                var returnType = typeof(TAction).GetMethod("Invoke").ReturnType;
                if (returnType == typeof(void))
                    returnType = null;

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
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentType).GetGenericMethod(nameof(ComponentType.ReadOnly), BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(ActionRequest<TAction>))));
                processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
                int offset = 1;
                if (HasVariable) {
                    processor.Emit(OpCodes.Dup);
                    processor.Emit(OpCodes.Ldc_I4, offset++);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentType).GetGenericMethod(nameof(ComponentType.ReadOnly), BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(ActionVariable<,>).MakeGenericType(typeof(TAction), variableType))));
                    processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
                }
                if (returnType != null) {
                    processor.Emit(OpCodes.Dup);
                    processor.Emit(OpCodes.Ldc_I4, offset++);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentType).GetGenericMethod(nameof(ComponentType.ReadWrite), BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(ActionResult<,>).MakeGenericType(typeof(TAction), returnType))));
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
                if (components != null) {
                    foreach (var component in components.Where(c => c.Value.singletonTarget)) {
                        processor.Emit(OpCodes.Ldarg_0);
                        processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.RequireSingletonForUpdate)).MakeGenericMethod(component.Value.componentType)));
                        processor.Emit(OpCodes.Nop);
                    }
                }


                //EntityCommandBufferSystem for entity cleanup if provided
                if (entityCommandBufferSystemField != null) {
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetProperty(nameof(SystemBase.World)).GetMethod));
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(World).GetGenericMethod(nameof(World.GetOrCreateSystem), BindingFlags.Public | BindingFlags.Instance).MakeGenericMethod(entityCommandBufferSystemType)));
                    processor.Emit(OpCodes.Stfld, entityCommandBufferSystemField);
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
                var initConfigInfoJob = new VariableDefinition(moduleDefinition.ImportReference(typeof(ActionExecutionConfigInitJob<TAction>)));
                onUpdateMethod.Body.Variables.Add(initConfigInfoJob);
                var executeActionJob = new VariableDefinition(jobDefinition.definition);
                onUpdateMethod.Body.Variables.Add(executeActionJob);
                var executeActionJobHandle = new VariableDefinition(moduleDefinition.ImportReference(typeof(JobHandle)));
                onUpdateMethod.Body.Variables.Add(executeActionJobHandle);
                VariableDefinition entityCommandBuffer = null;
                if (entityCommandBufferSystemField != null) {
                    entityCommandBuffer = new VariableDefinition(moduleDefinition.ImportReference(typeof(EntityCommandBuffer)));
                    onUpdateMethod.Body.Variables.Add(entityCommandBuffer);
                }
                VariableDefinition applyVariableJobHandle = null, applyVariableJob = null;
                if (HasVariable) {
                    applyVariableJobHandle = new VariableDefinition(moduleDefinition.ImportReference(typeof(JobHandle)));
                    onUpdateMethod.Body.Variables.Add(applyVariableJobHandle);
                    applyVariableJob = new VariableDefinition(moduleDefinition.ImportReference(typeof(ActionExecutionApplyVariableJob<,>).MakeGenericType(typeof(TAction), variableType)));
                    onUpdateMethod.Body.Variables.Add(applyVariableJob);
                }
                VariableDefinition fieldOperationsJobHandle = null, fieldOperationsJob = null;
                if (actionDefinitionAsset.useFieldOperations) {
                    fieldOperationsJobHandle = new VariableDefinition(moduleDefinition.ImportReference(typeof(JobHandle)));
                    onUpdateMethod.Body.Variables.Add(fieldOperationsJobHandle);
                    fieldOperationsJob = new VariableDefinition(moduleDefinition.ImportReference(typeof(ActionExecutionDoFieldOperations<TAction>)));
                    onUpdateMethod.Body.Variables.Add(fieldOperationsJob);
                }
                //Methods
                var systemState_executionRequestHandle = moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetComponentTypeHandle)).MakeGenericMethod(typeof(ActionRequest<TAction>)));
                var systemState_dependency = moduleDefinition.ImportReference(typeof(SystemBase).GetProperty("Dependency", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetMethod);
                var initConfigJob_schedule = moduleDefinition.ImportReference(typeof(JobEntityBatchExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public).First(m => m.Name == nameof(JobEntityBatchExtensions.Schedule) && m.GetParameters().Length == 3).MakeGenericMethod(typeof(ActionExecutionConfigInitJob<TAction>)));
                var executeActionJob_schedule = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(JobEntityBatchExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public).First(m => m.Name == nameof(JobEntityBatchExtensions.Schedule) && m.GetParameters().Length == 3)));
                executeActionJob_schedule.GenericArguments.Add(jobDefinition.definition);
                MethodReference systemState_variableRequestHandle = null;
                MethodReference applyVariableJob_schedule = null;
                MethodReference fieldOperationsRequestHandle = null;
                MethodReference fieldOperationsJob_schedule = null;
                //Fields
                var initConfigInfo_requestHandle = moduleDefinition.ImportReference(typeof(ActionExecutionConfigInitJob<TAction>).GetField(nameof(ActionExecutionConfigInitJob<TAction>.requestHandle)));
                var initConfigInfo_handles = moduleDefinition.ImportReference(typeof(ActionExecutionConfigInitJob<TAction>).GetField(nameof(ActionExecutionConfigInitJob<TAction>.handles)));
                FieldReference applyVariable_requestHandle = null, applyVariable_handles = null, applyVariable_variables = null;
                if (HasVariable) {
                    var variableJobType = typeof(ActionExecutionApplyVariableJob<,>).MakeGenericType(typeof(TAction), variableType);
                    var variableComponentType = typeof(ActionVariable<,>).MakeGenericType(typeof(TAction), variableType);
                    applyVariable_requestHandle = moduleDefinition.ImportReference(variableJobType.GetField("requestHandle"));
                    applyVariable_handles = moduleDefinition.ImportReference(variableJobType.GetField("handles"));
                    applyVariable_variables = moduleDefinition.ImportReference(variableJobType.GetField("variableHandle"));
                    systemState_variableRequestHandle = moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetComponentTypeHandle)).MakeGenericMethod(variableComponentType));
                    applyVariableJob_schedule = moduleDefinition.ImportReference(typeof(JobEntityBatchExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public).First(m => m.Name == nameof(JobEntityBatchExtensions.Schedule) && m.GetParameters().Length == 3).MakeGenericMethod(variableJobType));
                }
                FieldReference fieldOperations_requestHandle = null, fieldOperations_configHandles = null;
                if (actionDefinitionAsset.useFieldOperations) {
                    fieldOperationsRequestHandle = moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetComponentTypeHandle)).MakeGenericMethod(typeof(FieldOperationList))); ;
                    fieldOperationsJob_schedule = moduleDefinition.ImportReference(typeof(JobEntityBatchExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public).First(m => m.Name == nameof(JobEntityBatchExtensions.Schedule) && m.GetParameters().Length == 3).MakeGenericMethod(typeof(ActionExecutionDoFieldOperations<TAction>)));
                    fieldOperations_requestHandle = moduleDefinition.ImportReference(typeof(ActionExecutionDoFieldOperations<TAction>).GetField(nameof(ActionExecutionDoFieldOperations<TAction>.requestHandle)));
                    fieldOperations_configHandles = moduleDefinition.ImportReference(typeof(ActionExecutionDoFieldOperations<TAction>).GetField(nameof(ActionExecutionDoFieldOperations<TAction>.handles)));
                }

                VariableDefinition currentDependency = null;

                processor.Emit(OpCodes.Nop);
                processor.Emit(OpCodes.Ldloca, handles);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldflda, entityQueryField);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(EntityQuery).GetMethod(nameof(EntityQuery.CalculateEntityCount), Type.EmptyTypes)));
                processor.Emit(OpCodes.Ldc_I4, (int)Allocator.TempJob);
                processor.Emit(OpCodes.Ldc_I4_1);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(NativeArray<ConfigInfo>).GetConstructor(new Type[] { typeof(int), typeof(Allocator), typeof(NativeArrayOptions) })));
                //Init Config Handles Job
                processor.Emit(OpCodes.Ldloca, initConfigInfoJob);
                processor.Emit(OpCodes.Initobj, moduleDefinition.ImportReference(typeof(ActionExecutionConfigInitJob<TAction>)));
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
                    processor.Emit(OpCodes.Initobj, moduleDefinition.ImportReference(typeof(ActionExecutionApplyVariableJob<,>).MakeGenericType(typeof(TAction), variableType)));
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
                if (actionDefinitionAsset.useFieldOperations) {

                    processor.Emit(OpCodes.Ldloca, fieldOperationsJob);
                    processor.Emit(OpCodes.Initobj, moduleDefinition.ImportReference(typeof(ActionExecutionDoFieldOperations<TAction>)));
                    processor.Emit(OpCodes.Ldloca, fieldOperationsJob);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldc_I4_1);
                    processor.Emit(OpCodes.Call, systemState_executionRequestHandle);
                    processor.Emit(OpCodes.Stfld, fieldOperations_requestHandle);
                    processor.Emit(OpCodes.Ldloca, fieldOperationsJob);
                    processor.Emit(OpCodes.Ldloc, handles);
                    processor.Emit(OpCodes.Stfld, fieldOperations_configHandles);
                    processor.Emit(OpCodes.Ldloc, fieldOperationsJob);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, entityQueryField);
                    processor.Emit(OpCodes.Ldloc, currentDependency);
                    processor.Emit(OpCodes.Call, fieldOperationsJob_schedule);
                    processor.Emit(OpCodes.Stloc, fieldOperationsJobHandle);
                    currentDependency = fieldOperationsJobHandle;
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
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetComponentDataFromEntity)).MakeGenericMethod(typeof(ActionRequestAt<TAction>))));
                processor.Emit(OpCodes.Stfld, jobDefinition.requestAtDataField);
                processor.Emit(OpCodes.Ldloca, executeActionJob);
                processor.Emit(OpCodes.Ldc_I4, (int)Allocator.TempJob);
                processor.Emit(OpCodes.Newobj, moduleDefinition.ImportReference(typeof(NativeQueue<int>).GetConstructor(new Type[] { typeof(Allocator) })));
                processor.Emit(OpCodes.Stfld, jobDefinition.nodeQueueField);
                foreach (var actionParameter in jobDefinition.actionParameterFields.Where(p => p.componentType != null && p.fieldDefinition != null)) {
                    processor.Emit(OpCodes.Ldloca, executeActionJob);
                    processor.Emit(OpCodes.Ldarg_0);
                    if (actionParameter.variableType == ActionExecutionJobDefinition.ParameterInfo.VariableType.Singleton) {
                        processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetSingleton)).MakeGenericMethod(actionParameter.componentType)));
                        processor.Emit(OpCodes.Stfld, actionParameter.fieldDefinition);
                    }
                    else {
                        processor.Emit(OpCodes.Ldc_I4_1);
                        processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetComponentTypeHandle)).MakeGenericMethod(actionParameter.componentType)));
                        processor.Emit(OpCodes.Stfld, actionParameter.fieldDefinition);
                    }
                }
                if (jobDefinition.returnHandleField != null) {
                    processor.Emit(OpCodes.Ldloca, executeActionJob);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldc_I4_0);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetComponentTypeHandle)).MakeGenericMethod(typeof(ActionResult<,>).MakeGenericType(typeof(TAction), jobDefinition.returnType))));
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

                //Destroy entities if requested
                if (entityCommandBufferSystemField != null) {
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, entityCommandBufferSystemField);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(EntityCommandBufferSystem).GetMethod(nameof(EntityCommandBufferSystem.CreateCommandBuffer))));
                    processor.Emit(OpCodes.Stloc, entityCommandBuffer);
                    processor.Emit(OpCodes.Ldloc, entityCommandBuffer);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, entityQueryField);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(EntityCommandBuffer).GetMethod(nameof(EntityCommandBuffer.DestroyEntitiesForEntityQuery))));

                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, entityCommandBufferSystemField);
                    processor.Emit(OpCodes.Ldloc, executeActionJobHandle);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(EntityCommandBufferSystem).GetMethod(nameof(EntityCommandBufferSystem.AddJobHandleForProducer))));
                }
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
                    public enum VariableType : byte {
                        Normal = 0,
                        Singleton = 1,
                        ConfigHandle = 2,
                        ConfigHandleLength = 3,
                        ConfigOriginHandle = 4,
                        ConfigOriginHandleLength = 5,
                        Unassigned = 6

                    }
                    public VariableType variableType;
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
                public FieldDefinition nodeQueueField;
                public MethodDefinition executeMethod;
                public List<ParameterInfo> actionParameterFields;
                public FieldDefinition returnHandleField;
                public ActionExecutionJobDefinition(ModuleDefinition moduleDefinition, TypeDefinition containerTypeDefinition, ActionDefinitionAsset actionDefinitionAsset) {
                    this.definition = new TypeDefinition(string.Empty, "ActionExecutionJob", Mono.Cecil.TypeAttributes.NestedPublic | Mono.Cecil.TypeAttributes.SequentialLayout | Mono.Cecil.TypeAttributes.Sealed, moduleDefinition.ImportReference(typeof(ValueType)));
                    definition.Interfaces.Add(new InterfaceImplementation(moduleDefinition.ImportReference(typeof(IJobEntityBatch))));
                    definition.CustomAttributes.Add(new CustomAttribute(moduleDefinition.ImportReference(typeof(BurstCompileAttribute).GetConstructor(Type.EmptyTypes))));
                    var readOnlyAttributeReference = moduleDefinition.ImportReference(typeof(ReadOnlyAttribute).GetConstructor(Type.EmptyTypes));
                    this.returnType = typeof(TAction).GetMethod("Invoke").ReturnType;
                    this.returnTypeAggregator = actionDefinitionAsset.aggregator.IsCreated ? moduleDefinition.ImportReference(actionDefinitionAsset.aggregator.Value) : null;
                    requestHandleField = new FieldDefinition("requestHandle", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(typeof(ComponentTypeHandle<>).MakeGenericType(typeof(ActionRequest<TAction>))));
                    requestHandleField.CustomAttributes.Add(new CustomAttribute(readOnlyAttributeReference));
                    definition.Fields.Add(requestHandleField);
                    entityHandleField = new FieldDefinition("entityHandle", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(typeof(EntityTypeHandle)));
                    entityHandleField.CustomAttributes.Add(new CustomAttribute(readOnlyAttributeReference));
                    definition.Fields.Add(entityHandleField);
                    requestAtDataField = new FieldDefinition("requestAtData", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(typeof(ComponentDataFromEntity<>).MakeGenericType(typeof(ActionRequestAt<TAction>))));
                    requestAtDataField.CustomAttributes.Add(new CustomAttribute(readOnlyAttributeReference));
                    definition.Fields.Add(requestAtDataField);
                    configHandlesField = new FieldDefinition("configHandles", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(typeof(NativeArray<ConfigInfo>)));
                    configHandlesField.CustomAttributes.Add(new CustomAttribute(readOnlyAttributeReference));
                    definition.Fields.Add(configHandlesField);
                    nodeQueueField = new FieldDefinition("nodeQueue", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(typeof(NativeQueue<int>)));
                    nodeQueueField.CustomAttributes.Add(new CustomAttribute(moduleDefinition.ImportReference(typeof(DeallocateOnJobCompletionAttribute).GetConstructor(Type.EmptyTypes))));
                    definition.Fields.Add(nodeQueueField);
                    if (HasReturnType && HasReturnTypeAggregator) {
                        returnTypeComponent = typeof(ActionResult<,>).MakeGenericType(typeof(TAction), returnType);
                        returnTypeComponentReference = moduleDefinition.ImportReference(returnTypeComponent);
                        returnTypeComponentTypeHandleReference = moduleDefinition.ImportReference(typeof(ComponentTypeHandle<>).MakeGenericType(returnTypeComponent));
                        returnHandleField = new FieldDefinition("returnHandle", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(returnTypeComponentTypeHandleReference));
                        returnHandleField.CustomAttributes.Add(new CustomAttribute(moduleDefinition.ImportReference(typeof(WriteOnlyAttribute).GetConstructor(Type.EmptyTypes))));
                        definition.Fields.Add(returnHandleField);
                    }
                    var components = actionDefinitionAsset.GetComponents();
                    actionParameterFields = actionDefinitionAsset.delegateType.Value.GetMethod("Invoke").GetParameters().Select(parameter =>
                    {
                        if (parameter.ParameterType == typeof(ConfigDataLength)) {
                            return new ParameterInfo
                            {
                                //variableCurrent = new VariableDefinition(moduleDefinition.ImportReference(typeof(ConfigDataLength))),
                                parameterType = typeof(ConfigDataLength),
                                variableType = ParameterInfo.VariableType.ConfigHandleLength
                            };
                        }
                        else if (parameter.ParameterType == typeof(ConfigDataHandle)) {
                            return new ParameterInfo
                            {
                                variableCurrent = new VariableDefinition(moduleDefinition.ImportReference(typeof(ConfigDataHandle))),
                                parameterType = typeof(ConfigDataHandle),
                                variableType = ParameterInfo.VariableType.ConfigHandle
                            };
                        }
                        else if (components.TryGetValue(parameter.Name, out var component)) {
                            ParameterInfo info;
                            if (component.singletonTarget) {
                                info = new ParameterInfo
                                {
                                    fieldDefinition = new FieldDefinition($"{parameter.Name}_parameterHandle", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(component.componentType)),
                                    parameterType = parameter.ParameterType,
                                    fieldName = component.fieldName,
                                    singleton = component.singletonTarget,
                                    componentType = component.componentType,
                                    variableCurrent = new VariableDefinition(moduleDefinition.ImportReference(parameter.ParameterType)),
                                    variableType = ParameterInfo.VariableType.Singleton
                                };
                            }
                            else {
                                info = new ParameterInfo
                                {
                                    fieldDefinition = new FieldDefinition($"{parameter.Name}_parameterHandle", Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(typeof(ComponentTypeHandle<>).MakeGenericType(component.componentType))),
                                    parameterType = parameter.ParameterType,
                                    fieldName = component.fieldName,
                                    singleton = component.singletonTarget,
                                    componentType = component.componentType,
                                    variableCurrent = new VariableDefinition(moduleDefinition.ImportReference(parameter.ParameterType)),
                                    variableSource = new VariableDefinition(moduleDefinition.ImportReference(typeof(NativeArray<>).MakeGenericType(component.componentType))),
                                    variableType = ParameterInfo.VariableType.Normal
                                };
                            }

                            info.fieldDefinition.CustomAttributes.Add(new CustomAttribute(readOnlyAttributeReference));
                            definition.Fields.Add(info.fieldDefinition);
                            return info;
                        }
                        else {
                            return new ParameterInfo
                            {
                                parameterType = parameter.ParameterType,
                                singleton = component.singletonTarget,
                                variableCurrent = new VariableDefinition(moduleDefinition.ImportReference(parameter.ParameterType)),
                                variableType = ParameterInfo.VariableType.Unassigned
                            };
                        }
                    }).ToList();
                    GenerateExecute(moduleDefinition, actionDefinitionAsset);
                }
                private void GenerateExecute(ModuleDefinition moduleDefinition, ActionDefinitionAsset actionDefinitionAsset) {
                    //Type References
                    var nativeArray_entity = moduleDefinition.ImportReference(typeof(NativeArray<Entity>));
                    var nativeArray_actionExecutionRequest = moduleDefinition.ImportReference(typeof(NativeArray<ActionRequest<TAction>>));
                    var blobAssetReference_blobGraph = moduleDefinition.ImportReference(typeof(BlobAssetReference<ActionGraph<TAction>>));
                    var entityTypeHandle = moduleDefinition.ImportReference(typeof(EntityTypeHandle));
                    var componentTypeHandle_actionExecutionRequest = moduleDefinition.ImportReference(typeof(ComponentTypeHandle<ActionRequest<TAction>>));
                    var componentDataFromEntity_actionExecutionRequestAt = moduleDefinition.ImportReference(typeof(ComponentDataFromEntity<ActionRequestAt<TAction>>));
                    var blobGraphNode = moduleDefinition.ImportReference(typeof(ActionGraphNode<TAction>));
                    var functionPointer = moduleDefinition.ImportReference(typeof(FunctionPointer<TAction>));
                    var nativeQueue_int = moduleDefinition.ImportReference(typeof(NativeQueue<int>));
                    var archetypeChunk = moduleDefinition.ImportReference(typeof(ArchetypeChunk));
                    var blobArray_blobGraphNode = moduleDefinition.ImportReference(typeof(BlobArray<ActionGraphNode<TAction>>));
                    var configInfo = moduleDefinition.ImportReference(typeof(ConfigInfo));
                    var configHandle = moduleDefinition.ImportReference(typeof(ConfigDataHandle));
                    //Method References
                    var nativeArray_actionExecutionRequest_length = moduleDefinition.ImportReference(typeof(NativeArray<ActionRequest<TAction>>).GetProperty(nameof(NativeArray<ActionRequest<TAction>>.Length)).GetMethod);
                    var nativeArray_actionExecutionRequest_item = moduleDefinition.ImportReference(typeof(NativeArray<ActionRequest<TAction>>).GetProperty("Item").GetMethod);
                    var nativeArray_configInfo_item = moduleDefinition.ImportReference(typeof(NativeArray<ConfigInfo>).GetProperty("Item").GetMethod);
                    var blobAssetReference_blobGraph_value = moduleDefinition.ImportReference(typeof(BlobAssetReference<ActionGraph<TAction>>).GetProperty(nameof(BlobAssetReference<ActionGraph<TAction>>.Value)).GetMethod);
                    var blobAssetReference_blobGraph_isCreated = moduleDefinition.ImportReference(typeof(BlobAssetReference<ActionGraph<TAction>>).GetProperty(nameof(BlobAssetReference<ActionGraph<TAction>>.IsCreated)).GetMethod);
                    var nativeQueue_int_enqueue = moduleDefinition.ImportReference(typeof(NativeQueue<int>).GetMethod(nameof(NativeQueue<int>.Enqueue)));
                    var nativeQueue_int_dequeue = moduleDefinition.ImportReference(typeof(NativeQueue<int>).GetMethod(nameof(NativeQueue<int>.Dequeue)));
                    var nativeQueue_int_clear = moduleDefinition.ImportReference(typeof(NativeQueue<int>).GetMethod(nameof(NativeQueue<int>.Clear)));
                    var nativeQueue_int_isEmpty = moduleDefinition.ImportReference(typeof(NativeQueue<int>).GetMethod(nameof(NativeQueue<int>.IsEmpty)));
                    var componentDataFromEntity_actionExecutionRequestAt_hasComponent = moduleDefinition.ImportReference(typeof(ComponentDataFromEntity<ActionRequestAt<TAction>>).GetMethod(nameof(ComponentDataFromEntity<ActionRequestAt<TAction>>.HasComponent)));
                    var componentDataFromEntity_actionExecutionRequestAt_item = moduleDefinition.ImportReference(typeof(ComponentDataFromEntity<ActionRequestAt<TAction>>).GetProperty("Item").GetMethod);
                    var nativeArray_entity_item = moduleDefinition.ImportReference(typeof(NativeArray<Entity>).GetProperty("Item").GetMethod);

                    var archetypeChunk_getNativeArray_entityTypeHandle = moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetMethod(nameof(ArchetypeChunk.GetNativeArray), new Type[] { typeof(EntityTypeHandle) }));
                    var archetypeChunk_getNativeArray = moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetMethods(BindingFlags.Public | BindingFlags.Instance).First(m => m.Name == nameof(ArchetypeChunk.GetNativeArray) && m.IsGenericMethod).MakeGenericMethod(typeof(ActionRequest<TAction>)));
                    var configHandle_offset = moduleDefinition.ImportReference(typeof(ConfigHandleExtensions).GetMethod(nameof(ConfigHandleExtensions.CreateFromOffset), BindingFlags.Static | BindingFlags.Public));
                    MethodReference archetypeChunk_getNativeArray_returnTypeHandle = null;
                    MethodReference nativeArray_return_item_set = null;
                    if (HasReturnType && HasReturnTypeAggregator) {
                        var resultType = typeof(ActionResult<,>).MakeGenericType(typeof(TAction), returnType);
                        archetypeChunk_getNativeArray_returnTypeHandle = moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetMethods(BindingFlags.Public | BindingFlags.Instance).First(m => m.Name == nameof(ArchetypeChunk.GetNativeArray) && m.IsGenericMethod).MakeGenericMethod(resultType));
                        nativeArray_return_item_set = moduleDefinition.ImportReference(typeof(NativeArray<>).MakeGenericType(resultType).GetProperty("Item").SetMethod);
                    }
                    var blobArray_int_length = moduleDefinition.ImportReference(typeof(BlobArray<int>).GetProperty(nameof(BlobArray<int>.Length)).GetMethod);
                    var blobArray_int_item = moduleDefinition.ImportReference(typeof(BlobArray<int>).GetProperty("Item").GetMethod);
                    var blobArray_blobGraphNode_item = moduleDefinition.ImportReference(typeof(BlobArray<ActionGraphNode<TAction>>).GetProperty("Item").GetMethod);
                    var functionPointer_invoke = moduleDefinition.ImportReference(typeof(FunctionPointer<TAction>).GetProperty("Invoke").GetMethod);
                    //Fields
                    var actionExecutionRequestAt_startIndex = moduleDefinition.ImportReference(typeof(ActionRequestAt<TAction>).GetField(nameof(ActionRequestAt<TAction>.startIndex)));

                    var blobGraphNode_action = moduleDefinition.ImportReference(typeof(ActionGraphNode<TAction>).GetField(nameof(ActionGraphNode<TAction>.action)));
                    var blobGraphNode_configOffset = moduleDefinition.ImportReference(typeof(ActionGraphNode<TAction>).GetField(nameof(ActionGraphNode<TAction>.configOffset)));
                    var blobGraphNode_configLength = moduleDefinition.ImportReference(typeof(ActionGraphNode<TAction>).GetField(nameof(ActionGraphNode<TAction>.configOffset)));
                    var blobGraphNode_next = moduleDefinition.ImportReference(typeof(ActionGraphNode<TAction>).GetField(nameof(ActionGraphNode<TAction>.next)));
                    var blobGraph_roots = moduleDefinition.ImportReference(typeof(ActionGraph<TAction>).GetField(nameof(ActionGraph<TAction>.roots)));
                    var blobGraph_nodes = moduleDefinition.ImportReference(typeof(ActionGraph<TAction>).GetField(nameof(ActionGraph<TAction>.nodes)));
                    var actionExecutionRequest_value = moduleDefinition.ImportReference(typeof(ActionRequest<TAction>).GetField(nameof(ActionRequest<TAction>.value)));
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
                    executeMethod.CustomAttributes.Add(new CustomAttribute(moduleDefinition.ImportReference(typeof(BurstCompileAttribute).GetConstructor(Type.EmptyTypes))));
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
                    var actionRequestArrayVariable = new VariableDefinition(nativeArray_actionExecutionRequest);
                    var entityArrayVariable = new VariableDefinition(nativeArray_entity);
                    var var2 = new VariableDefinition(moduleDefinition.TypeSystem.Int32);
                    var graphVariable = new VariableDefinition(blobAssetReference_blobGraph);
                    var var4 = new VariableDefinition(moduleDefinition.TypeSystem.Boolean);
                    var var5 = new VariableDefinition(moduleDefinition.TypeSystem.Boolean);
                    var var6 = new VariableDefinition(moduleDefinition.TypeSystem.Int32);
                    var var7 = new VariableDefinition(moduleDefinition.TypeSystem.Boolean);
                    var currentNodeVariable = new VariableDefinition(blobGraphNode);
                    var var9 = new VariableDefinition(functionPointer);
                    var var10 = new VariableDefinition(moduleDefinition.TypeSystem.Boolean);
                    var var11 = new VariableDefinition(moduleDefinition.TypeSystem.Boolean);
                    var var12 = new VariableDefinition(moduleDefinition.TypeSystem.Boolean);

                    var currentConfigInfoVariable = new VariableDefinition(moduleDefinition.ImportReference(typeof(ConfigInfo)));
                    var firstNodeFlagVariable = new VariableDefinition(moduleDefinition.TypeSystem.Boolean);
                    VariableDefinition returnVar1 = null;
                    VariableDefinition returnVar2 = null;
                    VariableDefinition returnVarSource = null;
                    VariableDefinition returnVarComponent = null;
                    if (HasReturnType && HasReturnTypeAggregator) {
                        returnVar1 = new VariableDefinition(moduleDefinition.ImportReference(returnType));
                        returnVar2 = new VariableDefinition(moduleDefinition.ImportReference(returnType));
                        returnVarComponent = new VariableDefinition(moduleDefinition.ImportReference(returnTypeComponent));
                        returnVarSource = new VariableDefinition(moduleDefinition.ImportReference(typeof(NativeArray<>).MakeGenericType(typeof(ActionResult<,>).MakeGenericType(typeof(TAction), returnType))));
                    }
                    executeMethod.Body.Variables.Add(actionRequestArrayVariable);
                    executeMethod.Body.Variables.Add(entityArrayVariable);
                    executeMethod.Body.Variables.Add(var2);
                    executeMethod.Body.Variables.Add(graphVariable);
                    executeMethod.Body.Variables.Add(var4);
                    executeMethod.Body.Variables.Add(var5);
                    executeMethod.Body.Variables.Add(var6);
                    executeMethod.Body.Variables.Add(var7);
                    executeMethod.Body.Variables.Add(currentNodeVariable);
                    executeMethod.Body.Variables.Add(var9);
                    executeMethod.Body.Variables.Add(var10);
                    executeMethod.Body.Variables.Add(var11);
                    executeMethod.Body.Variables.Add(var12);
                    executeMethod.Body.Variables.Add(currentConfigInfoVariable);
                    executeMethod.Body.Variables.Add(firstNodeFlagVariable);

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
                    processor.Emit(OpCodes.Ldarga, batchInChunk);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, requestHandleField);
                    processor.Emit(OpCodes.Call, archetypeChunk_getNativeArray);
                    processor.Emit(OpCodes.Stloc_0);
                    // NativeArray<Entity> nativeArray2 = P_0.GetNativeArray(entityHandle);
                    processor.Emit(OpCodes.Ldarga, batchInChunk);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, entityHandleField);
                    processor.Emit(OpCodes.Call, archetypeChunk_getNativeArray_entityTypeHandle);
                    processor.Emit(OpCodes.Stloc_1);
                    if (HasReturnType && HasReturnTypeAggregator) {
                        processor.Emit(OpCodes.Ldarga, batchInChunk);
                        processor.Emit(OpCodes.Ldarg_0);
                        processor.Emit(OpCodes.Ldfld, returnHandleField);
                        processor.Emit(OpCodes.Call, archetypeChunk_getNativeArray_returnTypeHandle);
                        processor.Emit(OpCodes.Stloc, returnVarSource);
                    }
                    //Parameter Native Arrays
                    var parameterNativeArrayGetter = typeof(ArchetypeChunk).GetGenericMethod(nameof(ArchetypeChunk.GetNativeArray), BindingFlags.Public | BindingFlags.Instance);
                    foreach (var parameterInfo in actionParameterFields.Where(p => p.fieldDefinition != null && p.variableSource != null)) {

                        processor.Emit(OpCodes.Ldarga, batchInChunk);
                        processor.Emit(OpCodes.Ldarg_0);
                        processor.Emit(OpCodes.Ldfld, parameterInfo.fieldDefinition);
                        processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(parameterNativeArrayGetter.MakeGenericMethod(parameterInfo.componentType)));
                        processor.Emit(OpCodes.Stloc, parameterInfo.variableSource);
                    }
                    // for (int i = 0; i < nativeArray.Length; i++)
                    processor.Emit(OpCodes.Ldc_I4_0);
                    processor.Emit(OpCodes.Stloc_2);
                    processor.Emit(OpCodes.Br, bl_0158);
                    // BlobAssetReference<BlobGraph<SampleDelegate>> value = nativeArray[i].value;
                    processor.Append(bl_0024);
                    processor.Emit(OpCodes.Ldloca_S, actionRequestArrayVariable);
                    processor.Emit(OpCodes.Ldloc_2);
                    processor.Emit(OpCodes.Call, nativeArray_actionExecutionRequest_item);
                    processor.Emit(OpCodes.Ldfld, actionExecutionRequest_value);
                    processor.Emit(OpCodes.Stloc_3);
                    //Set flag
                    if (HasReturnType && HasReturnTypeAggregator) {
                        processor.Emit(OpCodes.Ldc_I4_1);
                        processor.Emit(OpCodes.Stloc, firstNodeFlagVariable);
                    }
                    //Get Parameters
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, configHandlesField);
                    processor.Emit(OpCodes.Ldloc_2);
                    processor.Emit(OpCodes.Call, nativeArray_configInfo_item);
                    processor.Emit(OpCodes.Stloc, currentConfigInfoVariable);
                    foreach (var parameterInfo in actionParameterFields) {
                        if (parameterInfo.parameterType == typeof(ConfigDataHandle)) {
                            processor.Emit(OpCodes.Ldloca_S, currentConfigInfoVariable);
                            processor.Emit(OpCodes.Ldfld, configHandle_handle);
                            processor.Emit(OpCodes.Stloc, parameterInfo.variableCurrent);
                        }
                        else if (parameterInfo.parameterType == typeof(ConfigDataLength)) {
                            processor.Emit(OpCodes.Ldloca_S, currentConfigInfoVariable);
                            processor.Emit(OpCodes.Ldfld, configHandle_length);
                            processor.Emit(OpCodes.Stloc, parameterInfo.variableCurrent);
                        }
                        else if (parameterInfo.variableSource != null) {
                            processor.Emit(OpCodes.Ldloca, parameterInfo.variableSource);
                            processor.Emit(OpCodes.Ldloc_2);
                            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(NativeArray<>).MakeGenericType(parameterInfo.componentType).GetProperty("Item").GetMethod));
                            processor.Emit(OpCodes.Ldfld, moduleDefinition.ImportReference(parameterInfo.componentType.GetField(parameterInfo.fieldName)));
                            processor.Emit(OpCodes.Stloc, parameterInfo.variableCurrent);
                        }

                    }

                    // if (!value.IsCreated)
                    processor.Emit(OpCodes.Ldloca, graphVariable);
                    processor.Emit(OpCodes.Call, blobAssetReference_blobGraph_isCreated);
                    processor.Emit(OpCodes.Stloc, var4);
                    processor.Emit(OpCodes.Ldloc, var4);
                    processor.Emit(OpCodes.Brfalse, bl_0153);

                    // if (requestAtData.HasComponent(nativeArray2[i]))
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, requestAtDataField);
                    processor.Emit(OpCodes.Ldloca, entityArrayVariable);
                    processor.Emit(OpCodes.Ldloc_2);
                    processor.Emit(OpCodes.Call, nativeArray_entity_item);
                    processor.Emit(OpCodes.Call, componentDataFromEntity_actionExecutionRequestAt_hasComponent);
                    processor.Emit(OpCodes.Stloc, var5);
                    processor.Emit(OpCodes.Ldloc, var5);
                    processor.Emit(OpCodes.Brfalse, bl_0085);
                    // nodeQueue.Enqueue(requestAtData[nativeArray2[i]].startIndex);
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, nodeQueueField);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, requestAtDataField);
                    processor.Emit(OpCodes.Ldloca, entityArrayVariable);
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
                    processor.Emit(OpCodes.Stloc, var6);
                    processor.Emit(OpCodes.Br, bl_00b3);
                    // nodeQueue.Enqueue(value.Value.roots[j]);
                    processor.Append(bl_008b);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, nodeQueueField);
                    processor.Emit(OpCodes.Ldloca, graphVariable);
                    processor.Emit(OpCodes.Call, blobAssetReference_blobGraph_value);
                    processor.Emit(OpCodes.Ldflda, blobGraph_roots);
                    processor.Emit(OpCodes.Ldloc, var6);
                    processor.Emit(OpCodes.Call, blobArray_int_item);
                    processor.Emit(OpCodes.Ldind_I4);
                    processor.Emit(OpCodes.Call, nativeQueue_int_enqueue);
                    processor.Emit(OpCodes.Nop);
                    // for (int j = 0; j < value.Value.roots.Length; j++)
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldloc, var6);
                    processor.Emit(OpCodes.Ldc_I4_1);
                    processor.Emit(OpCodes.Add); //IL_00B0
                    processor.Emit(OpCodes.Stloc, var6);
                    // for (int j = 0; j < value.Value.roots.Length; j++)
                    processor.Append(bl_00b3);
                    processor.Emit(OpCodes.Ldloca, graphVariable);
                    processor.Emit(OpCodes.Call, blobAssetReference_blobGraph_value);
                    processor.Emit(OpCodes.Ldflda, blobGraph_roots);
                    processor.Emit(OpCodes.Call, blobArray_int_length);
                    processor.Emit(OpCodes.Clt);
                    processor.Emit(OpCodes.Stloc, var7);
                    processor.Emit(OpCodes.Ldloc, var7);
                    processor.Emit(OpCodes.Brtrue, bl_008b);
                    processor.Emit(OpCodes.Nop);
                    processor.Append(bl_00cf);


                    processor.Append(bl_00d1);

                    processor.Emit(OpCodes.Ldloca, graphVariable);
                    processor.Emit(OpCodes.Call, blobAssetReference_blobGraph_value);
                    processor.Emit(OpCodes.Ldflda, blobGraph_nodes);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, nodeQueueField);
                    processor.Emit(OpCodes.Call, nativeQueue_int_dequeue);
                    processor.Emit(OpCodes.Call, blobArray_blobGraphNode_item);
                    processor.Emit(OpCodes.Ldobj, blobGraphNode);
                    processor.Emit(OpCodes.Stloc, currentNodeVariable);
                    //Call
                    processor.Emit(OpCodes.Ldloc, currentNodeVariable);
                    processor.Emit(OpCodes.Ldfld, blobGraphNode_action);
                    processor.Emit(OpCodes.Call, functionPointer_invoke);
                    foreach (var parameterInfo in actionParameterFields) {
                        switch (parameterInfo.variableType) {
                            case ParameterInfo.VariableType.Singleton:
                                processor.Emit(OpCodes.Ldarg_0);
                                processor.Emit(OpCodes.Ldfld, parameterInfo.fieldDefinition);
                                processor.Emit(OpCodes.Ldfld, moduleDefinition.ImportReference(parameterInfo.componentType.GetField(parameterInfo.fieldName)));
                                break;
                            case ParameterInfo.VariableType.ConfigHandle:
                                processor.Emit(OpCodes.Ldloc, parameterInfo.variableCurrent);
                                processor.Emit(OpCodes.Ldloc, currentNodeVariable);
                                processor.Emit(OpCodes.Ldfld, blobGraphNode_configOffset);
                                processor.Emit(OpCodes.Call, configHandle_offset);
                                break;
                            case ParameterInfo.VariableType.ConfigHandleLength:
                                processor.Emit(OpCodes.Ldloc, currentNodeVariable);
                                processor.Emit(OpCodes.Ldfld, blobGraphNode_configLength);
                                break;
                            case ParameterInfo.VariableType.ConfigOriginHandle:
                                break;
                            case ParameterInfo.VariableType.ConfigOriginHandleLength:
                                break;
                            case ParameterInfo.VariableType.Unassigned:
                            case ParameterInfo.VariableType.Normal:
                                processor.Emit(OpCodes.Ldloc, parameterInfo.variableCurrent);
                                break;
                            default:
                                break;
                        }
                    }
                    processor.Emit(OpCodes.Callvirt, moduleDefinition.ImportReference(typeof(TAction).GetMethod("Invoke")));
                    if (HasReturnType) {
                        if (HasReturnTypeAggregator) {
                            processor.Emit(OpCodes.Stloc, returnVar2);
                            var br1 = processor.Create(OpCodes.Nop);
                            var br2 = processor.Create(OpCodes.Nop);
                            processor.Emit(OpCodes.Ldloc, firstNodeFlagVariable);
                            processor.Emit(OpCodes.Brfalse, br1);
                            processor.Emit(OpCodes.Ldloc, returnVar2);
                            processor.Emit(OpCodes.Stloc, returnVar1);
                            processor.Emit(OpCodes.Ldc_I4_0);
                            processor.Emit(OpCodes.Stloc, firstNodeFlagVariable);
                            processor.Emit(OpCodes.Br, br2);
                            processor.Append(br1);
                            processor.Emit(OpCodes.Ldloc, returnVar1);
                            processor.Emit(OpCodes.Ldloc, returnVar2);
                            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(returnTypeAggregator));
                            processor.Emit(OpCodes.Stloc, returnVar1);
                            processor.Append(br2);
                        }
                        else {
                            processor.Emit(OpCodes.Pop);
                        }
                    }
                    processor.Emit(OpCodes.Nop);

                    //Inject return update
                    processor.Emit(OpCodes.Ldloc, currentNodeVariable);
                    processor.Emit(OpCodes.Ldfld, blobGraphNode_next);
                    processor.Emit(OpCodes.Ldc_I4_0);
                    processor.Emit(OpCodes.Clt);
                    processor.Emit(OpCodes.Ldc_I4_0);
                    processor.Emit(OpCodes.Ceq);
                    processor.Emit(OpCodes.Stloc, var10);
                    processor.Emit(OpCodes.Ldloc, var10);
                    processor.Emit(OpCodes.Brfalse, bl_0131);
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, nodeQueueField);
                    processor.Emit(OpCodes.Ldloc, currentNodeVariable);
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
                    processor.Emit(OpCodes.Stloc, var11);
                    processor.Emit(OpCodes.Ldloc, var11);
                    processor.Emit(OpCodes.Brtrue, bl_00d1);
                    //Update result
                    if (HasReturnType && HasReturnTypeAggregator) {
                        processor.Emit(OpCodes.Ldloca, returnVarSource);
                        processor.Emit(OpCodes.Ldloc_2);
                        processor.Emit(OpCodes.Ldloca, returnVarComponent);
                        processor.Emit(OpCodes.Initobj, returnTypeComponentReference);
                        processor.Emit(OpCodes.Ldloca, returnVarComponent);
                        processor.Emit(OpCodes.Ldloc, returnVar1);
                        processor.Emit(OpCodes.Stfld, return_value);
                        processor.Emit(OpCodes.Ldloc, returnVarComponent);
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
                    processor.Emit(OpCodes.Ldloca, actionRequestArrayVariable);
                    processor.Emit(OpCodes.Call, nativeArray_actionExecutionRequest_length);
                    processor.Emit(OpCodes.Clt);
                    processor.Emit(OpCodes.Stloc, var12);
                    processor.Emit(OpCodes.Ldloc, var12);
                    processor.Emit(OpCodes.Brtrue, bl_0024);
                    processor.Emit(OpCodes.Ret);
                    executeMethod.Body.OptimizeMacros();
                }
            }
        }
    }
}