using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using AOT;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using NeroWeNeed.Commons.AssemblyAnalyzers.Editor;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

[assembly: AdditionalAssemblyAnalysisPath(NeroWeNeed.ActionGraph.Editor.CodeGen.FieldOperationProducer.Output)]

namespace NeroWeNeed.ActionGraph.Editor.CodeGen {
    public static class FieldOperationProducer {
        public const string AssemblyName = "NeroWeNeed.ActionGraph.FieldOperations";
        public const string Output = "Packages/github.neroweneed.action-graph/" + AssemblyName + ".dll";
        private static TypeReference typeReference;
        private static Type type;
        private static readonly InstructionData[] Types = new InstructionData[] {
                new InstructionData(typeof(byte)),
                new InstructionData(typeof(ushort)),
                new InstructionData(typeof(uint)),
                new InstructionData(typeof(ulong)),
                new InstructionData(typeof(sbyte),Instruction.Create(OpCodes.Stind_I1),Instruction.Create(OpCodes.Conv_I1)),
                new InstructionData(typeof(short),Instruction.Create(OpCodes.Stind_I2),Instruction.Create(OpCodes.Conv_I2)),
                new InstructionData(typeof(int),Instruction.Create(OpCodes.Stind_I4),Instruction.Create(OpCodes.Conv_I4)),
                new InstructionData(typeof(long),Instruction.Create(OpCodes.Stind_I8),Instruction.Create(OpCodes.Conv_I8)),
                new InstructionData(typeof(float),Instruction.Create(OpCodes.Stind_R4),Instruction.Create(OpCodes.Conv_R4)),
                new InstructionData(typeof(double),Instruction.Create(OpCodes.Stind_R8),Instruction.Create(OpCodes.Conv_R8))
        };
        private class InstructionData {
            public Type type;
            public TypeReference typeReference;
            public Instruction storeInstruction;
            public Instruction conversionInstruction;
            public TypeDefinition binaryConfigTypeDefinition;
            public TypeDefinition unaryConfigTypeDefinition;

            public InstructionData(Type type, Instruction storeInstruction = null, Instruction conversionInstruction = null) {
                this.type = type;
                this.storeInstruction = storeInstruction;
                this.typeReference = null;
                this.conversionInstruction = conversionInstruction;
                this.binaryConfigTypeDefinition = null;
                this.unaryConfigTypeDefinition = null;
            }
            public Instruction GetStoreInstruction(ModuleDefinition moduleDefinition) {
                return storeInstruction ??= Instruction.Create(OpCodes.Stobj, moduleDefinition.ImportReference(type));
            }
            public TypeDefinition GetBinaryConfigTypeDefinition(ModuleDefinition moduleDefinition) {
                return binaryConfigTypeDefinition ??= CreateBinaryConfigType(moduleDefinition, this);
            }
            public TypeDefinition GetUnaryConfigTypeDefinition(ModuleDefinition moduleDefinition) {
                return unaryConfigTypeDefinition ??= CreateUnaryConfigType(moduleDefinition, this);
            }
            public TypeReference GetTypeReference(ModuleDefinition moduleDefinition) {
                return typeReference ??= moduleDefinition.ImportReference(type);
            }
        }
        [MenuItem("Assets/Generate Field Operations Assembly")]
        public static void CreateAssembly() {
            using var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory("Library/ScriptAssemblies");
            using (var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(AssemblyName, new Version(1, 0, 0, 0)), "NeroWeNeed.ActionGraph.FieldTransformers", new ModuleParameters { Kind = ModuleKind.Dll, AssemblyResolver = resolver, Runtime = TargetRuntime.Net_4_0, })) {
                type = Type.GetType("System.Type");
                typeReference = assembly.MainModule.ImportReference(type);
                var operations = new TypeDefinition("NeroWeNeed.ActionGraph.FieldOperations", "Operations", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Sealed | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Class, assembly.MainModule.TypeSystem.Object);
                operations.CustomAttributes.Add(new CustomAttribute(assembly.MainModule.ImportReference(typeof(BurstCompileAttribute).GetConstructor(Type.EmptyTypes))));
                foreach (var type in Types) {
                    AppendBinaryOperationSet(assembly.MainModule, operations, type);
                    if (type.conversionInstruction != null) {
                        AppendConversionOperationSet(assembly.MainModule, operations, type);
                    }
                }
                AppendMathOperationSet(assembly.MainModule, operations);
                assembly.MainModule.Types.Add(operations);
                assembly.Write(Output);
            }
            AssetDatabase.ImportAsset(Output);
        }
        private static void AppendMathOperationSet(ModuleDefinition moduleDefinition, TypeDefinition definition) {
            var configTypes = new Dictionary<TypesKey, TypeDefinition>();
            foreach (var method in typeof(math).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Where(m => ValidMethod(m))) {
                var key = new TypesKey(method.GetParameters().Select(p => p.ParameterType).ToArray());
                if (!configTypes.TryGetValue(key, out TypeDefinition type)) {
                    type = CreateConfigType(moduleDefinition, method);
                    configTypes[key] = type;
                }
                AppendMathOperation(moduleDefinition, definition, type, method);
            }
        }
        private static void AppendMathOperation(ModuleDefinition moduleDefinition, TypeDefinition container, TypeDefinition config, MethodInfo methodInfo) {
            var method = FieldOperationMethod.Create(moduleDefinition, container, $"MathOperation_{methodInfo.Name}_{config.Name}", moduleDefinition.ImportReference(methodInfo.ReturnType), config, $"core_field_operation_math_{methodInfo.Name}", CultureInfo.InvariantCulture.TextInfo.ToTitleCase(methodInfo.Name), GetSubIdentifier(methodInfo));
            method.definition.Body.InitLocals = true;
            method.definition.Body.SimplifyMacros();
            var data = new VariableDefinition(config);
            method.definition.Body.Variables.Add(data);
            var processor = method.definition.Body.GetILProcessor();
            processor.Emit(OpCodes.Nop);
            processor.Emit(OpCodes.Ldarga_S, method.configParameter);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(IntPtr).GetMethod(nameof(IntPtr.ToPointer))));
            var call = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(UnsafeUtility).GetMethod(nameof(UnsafeUtility.AsRef))));
            call.GenericArguments.Add(config);
            processor.Emit(OpCodes.Call, call);
            processor.Emit(OpCodes.Ldobj, config);
            processor.Emit(OpCodes.Stloc, data);
            processor.Emit(OpCodes.Ldarg_1);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(IntPtr).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance).First(m => m.Name == "op_Explicit" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IntPtr) && m.ReturnType == typeof(void*))));
            foreach (var field in config.Fields) {
                processor.Emit(OpCodes.Ldloc_0);
                processor.Emit(OpCodes.Ldfld, field);
            }
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(methodInfo));
            processor.Emit(OpCodes.Stobj, moduleDefinition.ImportReference(methodInfo.ReturnType));
            processor.Emit(OpCodes.Ret);
            method.definition.Body.OptimizeMacros();
        }
        private static bool ValidMethod(MethodInfo methodInfo) {
            return UnsafeUtility.IsUnmanaged(methodInfo.ReturnType) && methodInfo.ReturnType != typeof(void) && !methodInfo.ReturnType.IsPointer && methodInfo.GetParameters().Length > 0 && methodInfo.GetParameters().All(p => ValidMethodParameter(p));
        }
        private static bool ValidMethodParameter(ParameterInfo parameterInfo) {
            return UnsafeUtility.IsUnmanaged(parameterInfo.ParameterType) && !parameterInfo.IsIn && !parameterInfo.IsOut && !parameterInfo.ParameterType.IsPointer;
        }
        private static string GetSubIdentifier(MethodInfo methodInfo) {
            var sb = new StringBuilder();
            foreach (var parameter in methodInfo.GetParameters()) {
                sb.Append($"{parameter.ParameterType.Name},");
            }
            sb.Append(methodInfo.ReturnType.Name);
            return sb.ToString();
        }
        private static void AppendBinaryOperationSet(ModuleDefinition moduleDefinition, TypeDefinition definition, InstructionData instructionData) {
            AppendBinaryOperation(moduleDefinition, definition, instructionData, "Add", Instruction.Create(OpCodes.Add));
            AppendBinaryOperation(moduleDefinition, definition, instructionData, "Subtract", Instruction.Create(OpCodes.Sub));
            AppendBinaryOperation(moduleDefinition, definition, instructionData, "Multiply", Instruction.Create(OpCodes.Mul));
            AppendBinaryOperation(moduleDefinition, definition, instructionData, "Divide", Instruction.Create(OpCodes.Div));
            AppendBinaryOperation(moduleDefinition, definition, instructionData, "Modulos", Instruction.Create(OpCodes.Rem));

        }
        private static void AppendConversionOperationSet(ModuleDefinition moduleDefinition, TypeDefinition definition, InstructionData instructionData) {
            var unaryOpData = CreateUnaryConfigType(moduleDefinition, instructionData);
            foreach (var type in Types.Where((t) => !t.Equals(instructionData))) {
                AppendConversionOperation(moduleDefinition, definition, instructionData, unaryOpData, type);
            }

        }
        private static string GetConfigTypeName(MethodInfo method) {
            var sb = new StringBuilder("OperationData");
            foreach (var parameter in method.GetParameters()) {
                sb.Append($"_{parameter.ParameterType.Name}");
            }
            return sb.ToString();
        }
        private static TypeDefinition CreateConfigType(ModuleDefinition moduleDefinition, MethodInfo method) {

            var data = new TypeDefinition("NeroWeNeed.ActionGraph.FieldOperations.Data", GetConfigTypeName(method), Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.SequentialLayout | Mono.Cecil.TypeAttributes.Sealed, moduleDefinition.ImportReference(typeof(ValueType)));
            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++) {
                data.Fields.Add(new FieldDefinition(parameters[i].Name, Mono.Cecil.FieldAttributes.Public, moduleDefinition.ImportReference(parameters[i].ParameterType)));
            }

            moduleDefinition.Types.Add(data);
            return data;
        }
        private static TypeDefinition CreateBinaryConfigType(ModuleDefinition moduleDefinition, InstructionData data) {
            var binaryOpData = new TypeDefinition("NeroWeNeed.ActionGraph.FieldOperations.Data", $"BinaryOperationData_{data.type.Name}", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.SequentialLayout | Mono.Cecil.TypeAttributes.Sealed, moduleDefinition.ImportReference(typeof(ValueType)));
            binaryOpData.Fields.Add(new FieldDefinition("a", Mono.Cecil.FieldAttributes.Public, data.GetTypeReference(moduleDefinition)));
            binaryOpData.Fields.Add(new FieldDefinition("b", Mono.Cecil.FieldAttributes.Public, data.GetTypeReference(moduleDefinition)));
            moduleDefinition.Types.Add(binaryOpData);
            return binaryOpData;

        }
        private static TypeDefinition CreateUnaryConfigType(ModuleDefinition moduleDefinition, InstructionData data) {
            var unaryOpData = new TypeDefinition("NeroWeNeed.ActionGraph.FieldOperations.Data", $"UnaryOperationData_{data.type.Name}", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.SequentialLayout | Mono.Cecil.TypeAttributes.Sealed, moduleDefinition.ImportReference(typeof(ValueType)));
            unaryOpData.Fields.Add(new FieldDefinition("a", Mono.Cecil.FieldAttributes.Public, data.GetTypeReference(moduleDefinition)));
            moduleDefinition.Types.Add(unaryOpData);
            return unaryOpData;
        }
        private static void AppendConversionOperation(ModuleDefinition moduleDefinition, TypeDefinition definition, InstructionData instructionData, TypeDefinition config, InstructionData from) {
            var method = FieldOperationMethod.Create(moduleDefinition, definition, $"ConvertTo{instructionData.type.Name}From{from.type.Name}", instructionData.GetTypeReference(moduleDefinition), from.GetUnaryConfigTypeDefinition(moduleDefinition), $"core_field_operation_convert_to_{instructionData.type.Name.ToLower()}", $"To{instructionData.type.Name}", from.type.Name.ToLower());
            method.definition.Body.InitLocals = true;
            method.definition.Body.SimplifyMacros();
            var data = new VariableDefinition(from.GetUnaryConfigTypeDefinition(moduleDefinition));
            method.definition.Body.Variables.Add(data);
            var processor = method.definition.Body.GetILProcessor();
            processor.Emit(OpCodes.Nop);
            processor.Emit(OpCodes.Ldarga_S, method.configParameter);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(IntPtr).GetMethod(nameof(IntPtr.ToPointer))));
            var call = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(UnsafeUtility).GetMethod(nameof(UnsafeUtility.AsRef))));
            call.GenericArguments.Add(from.GetUnaryConfigTypeDefinition(moduleDefinition));
            processor.Emit(OpCodes.Call, call);
            processor.Emit(OpCodes.Ldobj, from.GetUnaryConfigTypeDefinition(moduleDefinition));
            processor.Emit(OpCodes.Stloc, data);
            processor.Emit(OpCodes.Ldarg_1);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(IntPtr).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance).First(m => m.Name == "op_Explicit" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IntPtr) && m.ReturnType == typeof(void*))));
            processor.Emit(OpCodes.Ldloc_0);
            processor.Emit(OpCodes.Ldfld, from.GetUnaryConfigTypeDefinition(moduleDefinition).Fields[0]);
            processor.Append(instructionData.conversionInstruction);
            processor.Append(instructionData.GetStoreInstruction(moduleDefinition));
            processor.Emit(OpCodes.Ret);
            method.definition.Body.OptimizeMacros();

        }
        private static void AppendBinaryOperation(ModuleDefinition moduleDefinition, TypeDefinition definition, InstructionData instructionData, string baseName, Instruction operation) {
            var method = FieldOperationMethod.Create(moduleDefinition, definition, $"{baseName}{instructionData.type.Name}", instructionData.GetTypeReference(moduleDefinition), instructionData.GetBinaryConfigTypeDefinition(moduleDefinition), $"core_field_operation_{baseName.ToLower()}", baseName, instructionData.type.Name.ToLower());
            method.definition.Body.InitLocals = true;
            method.definition.Body.SimplifyMacros();
            var data = new VariableDefinition(instructionData.GetBinaryConfigTypeDefinition(moduleDefinition));
            method.definition.Body.Variables.Add(data);
            var processor = method.definition.Body.GetILProcessor();
            processor.Emit(OpCodes.Nop);
            processor.Emit(OpCodes.Ldarga_S, method.configParameter);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(IntPtr).GetMethod(nameof(IntPtr.ToPointer))));
            var call = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(UnsafeUtility).GetMethod(nameof(UnsafeUtility.AsRef))));
            call.GenericArguments.Add(instructionData.GetBinaryConfigTypeDefinition(moduleDefinition));
            processor.Emit(OpCodes.Call, call);
            processor.Emit(OpCodes.Ldobj, instructionData.GetBinaryConfigTypeDefinition(moduleDefinition));
            processor.Emit(OpCodes.Stloc, data);
            processor.Emit(OpCodes.Ldarg_1);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(IntPtr).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance).First(m => m.Name == "op_Explicit" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IntPtr) && m.ReturnType == typeof(void*))));
            processor.Emit(OpCodes.Ldloc_0);
            processor.Emit(OpCodes.Ldfld, instructionData.GetBinaryConfigTypeDefinition(moduleDefinition).Fields[0]);
            processor.Emit(OpCodes.Ldloc_0);
            processor.Emit(OpCodes.Ldfld, instructionData.GetBinaryConfigTypeDefinition(moduleDefinition).Fields[1]);
            processor.Append(operation);
            processor.Append(instructionData.GetStoreInstruction(moduleDefinition));
            processor.Emit(OpCodes.Ret);
            method.definition.Body.OptimizeMacros();
        }
        private struct TypesKey : IEquatable<TypesKey> {
            public Type[] types;

            public TypesKey(Type[] types) {
                this.types = types;
            }

            public override bool Equals(object obj) {
                return obj is TypesKey other && Equals(other);
            }
            public bool Equals(TypesKey other) {
                return Enumerable.SequenceEqual(types, other.types);
            }

            public override int GetHashCode() {
                int hash = 627613354;
                if (types != null) {
                    foreach (var t in types) {
                        hash += t.GetHashCode();
                    }
                }

                return hash;

            }
        }

        private class FieldOperationMethod {
            public MethodDefinition definition;
            public ParameterDefinition configParameter;
            public ParameterDefinition outputParameter;

            public static FieldOperationMethod Create(ModuleDefinition moduleDefinition, TypeDefinition container, string name, TypeReference returnType, TypeDefinition config, string identifier, string displayName, string subIdentifier) {
                var method = new FieldOperationMethod
                {
                    definition = new MethodDefinition(name, Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.Public, moduleDefinition.TypeSystem.Void),
                    configParameter = new ParameterDefinition(moduleDefinition.TypeSystem.IntPtr),
                    outputParameter = new ParameterDefinition(moduleDefinition.TypeSystem.IntPtr)
                };
                method.definition.Parameters.Add(method.configParameter);
                method.definition.Parameters.Add(method.outputParameter);
                method.definition.CustomAttributes.Add(new CustomAttribute(moduleDefinition.ImportReference(typeof(BurstCompileAttribute).GetConstructor(Type.EmptyTypes))));
                var attr = new CustomAttribute(moduleDefinition.ImportReference(typeof(FieldOperationAttribute).GetConstructors()[0]));
                attr.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.TypeSystem.String, identifier));
                attr.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.ImportReference(Type.GetType("System.Type")), config));
                attr.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.TypeSystem.String, displayName));
                attr.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.TypeSystem.String, subIdentifier));
                attr.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.ImportReference(Type.GetType("System.Type")), returnType));

                method.definition.CustomAttributes.Add(attr);
                var pInvoke = new CustomAttribute(moduleDefinition.ImportReference(typeof(MonoPInvokeCallbackAttribute).GetConstructors()[0]));
                pInvoke.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.ImportReference(Type.GetType("System.Type")), moduleDefinition.ImportReference(typeof(FieldOperation))));
                method.definition.CustomAttributes.Add(pInvoke);
                container.Methods.Add(method.definition);
                return method;
            }
        }

    }
}