using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using NeroWeNeed.Commons.Editor;
using UnityEditor;

namespace NeroWeNeed.ActionGraph.Editor.CodeGen {
    //Responsible for generating editor code such as menus
    public static class ActionEditorUtilityProducer {
        public const string GeneratedNamespaceExtension = "Generated";
        public const string ClassName = "ActionEditorUtilities";
        public const string AssemblyName = "NeroWeNeed.ActionGraph.ActionEditorUtilities";

        public const string ActionAssetMenuTypeName = "ActionAssetMenus";
        [MenuItem("Assets/Create ActionGraph Editor DLL")]
        public static void CreateAssembly() {
            var settings = ProjectUtility.GetOrCreateProjectSettings<ActionGraphGlobalSettings>();
            var output = settings.actionSystemsDLLDirectory + "/" + AssemblyName + ".dll";
            if (!Directory.Exists(settings.actionSystemsDLLDirectory)) {
                Directory.CreateDirectory(settings.actionSystemsDLLDirectory);
            }
            var definitions = ActionDefinitionAsset.LoadAll().ToArray();
            using var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory("Library/ScriptAssemblies");
            using (var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(AssemblyName, new Version(1, 0, 0, 0)), AssemblyName, new ModuleParameters { Kind = ModuleKind.Dll, AssemblyResolver = resolver, Runtime = TargetRuntime.Net_4_0, })) {
                var actionAssetMenusType = new TypeDefinition("NeroWeNeed.ActionGraph.Generated", ActionAssetMenuTypeName, TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed, assembly.MainModule.TypeSystem.Object);
                GenerateMenus(assembly, assembly.MainModule, actionAssetMenusType, definitions);
                assembly.MainModule.Types.Add(actionAssetMenusType);
                assembly.Write(output);
            }
            AssetDatabase.ImportAsset(output);
        }
        private static void GenerateMenus(AssemblyDefinition assemblyDefinition, ModuleDefinition moduleDefinition, TypeDefinition typeDefinition, ActionDefinitionAsset[] definitionAssets) {
            var attrConstructor = moduleDefinition.ImportReference(typeof(MenuItem).GetConstructor(new Type[] { typeof(string) }));
            var createAssetMethod = moduleDefinition.ImportReference(typeof(ActionAssetEditor).GetMethod(nameof(ActionAssetEditor.CreateAsset), new Type[] { typeof(string) }));
            foreach (var definition in definitionAssets) {
                GenerateMenu(assemblyDefinition, moduleDefinition, typeDefinition, definition, attrConstructor, createAssetMethod);
            }
        }
        private static void GenerateMenu(AssemblyDefinition assemblyDefinition, ModuleDefinition moduleDefinition, TypeDefinition typeDefinition, ActionDefinitionAsset definitionAsset, MethodReference attrConstructor, MethodReference createAssetMethod) {
            var method = new MethodDefinition($"CreateActionAsset_{definitionAsset.id.guid}", MethodAttributes.Private | MethodAttributes.Static, moduleDefinition.TypeSystem.Void);
            var attr = new CustomAttribute(attrConstructor);
            attr.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.TypeSystem.String, $"Assets/Create/Actions/{definitionAsset.Name}"));
            method.CustomAttributes.Add(attr);
            method.Body.InitLocals = true;
            method.Body.SimplifyMacros();
            var processor = method.Body.GetILProcessor();
            processor.Emit(OpCodes.Ldstr, definitionAsset.id.guid);
            processor.Emit(OpCodes.Call, createAssetMethod);
            processor.Emit(OpCodes.Ret);
            method.Body.OptimizeMacros();
            typeDefinition.Methods.Add(method);
        }
    }

}