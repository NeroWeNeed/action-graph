using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using NeroWeNeed.Commons.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEngine;

namespace NeroWeNeed.ActionGraph.Editor {
    public static class CodeGen {
        private const string TargetAssembly = "Library/ScriptAssemblies/Assembly-CSharp.dll";
        //private const string TargetAssembly = "Library/ScriptAssemblies/Core.dll";
        /*         [MenuItem("CodeGen/Sample")]
                [InitializeOnLoadMethod]
                public static void Generate() {
                    var asset = ProjectGlobalSettingsUtility.GetSettings<ActionGraphGlobalSettings>();
                    if (asset != null) {
                        EditorApplication.LockReloadAssemblies();
                        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(TargetAssembly, new ReaderParameters { ReadWrite = true })) {


                            foreach (var actionInfo in asset.actions) {
                                if (string.IsNullOrWhiteSpace(actionInfo.identifier))
                                    continue;
                                var actionGraphAssetType = typeof(ActionGraphAsset<>).MakeGenericType(actionInfo.delegateType);
                                var actionGraphAssetTypeReference = assembly.MainModule.ImportReference(actionGraphAssetType);
                                var name = $"{System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase($"{actionInfo.identifier}")}Action";
                                Debug.Log(name);
                                TypeDefinition type = new TypeDefinition(asset.codeGenNamespace, name, Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public, actionGraphAssetTypeReference);
                                assembly.MainModule.Types.Add(type);
                            }
                            assembly.Write();
                        }
                        EditorApplication.UnlockReloadAssemblies();
                        Debug.Log("generated");
                    }

                } */


/*         [MenuItem("CodeGen/Sample")]
        [PostProcessBuild(0)]
        [InitializeOnLoadMethod]
        public static void Generate() {
            //EditorApplication.LockReloadAssemblies();
            CompilationPipeline.assemblyCompilationFinished += Generate;
        }
        private static void Generate(string path, CompilerMessage[] messages) {
            //EditorApplication.LockReloadAssemblies();
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path,new ReaderParameters { ReadWrite = true});
            var module = assembly.MainModule;
            var delegateType = module.ImportReference(typeof(Delegate));
            var assetType = module.ImportReference(typeof(ActionGraphAsset<>));
            var attrName = "NeroWeNeed.ActionGraph.ActionDefinition";
            var types = new List<TypeDefinition>();
            foreach (var type in module.Types) {
                if (!type.HasCustomAttributes)
                    continue;

                foreach (var attr in type.CustomAttributes) {
                    if (attr.AttributeType.FullName == attrName) {
                        var baseType = assetType.MakeGenericInstanceType(type);
                        Debug.Log(type);
                        var name = $"{type.Name}Action";
                        var typeDef = new TypeDefinition(type.Namespace, name, TypeAttributes.Public | TypeAttributes.Class, baseType);
                        //module.Types.Add(typeDef);
                        Debug.Log($"Delegate Found--: {type} [{type.BaseType}]");
                    }
                }
            }
            if (types.Count > 0) {
                foreach (var t in types) {
                    module.Types.Add(t);
                }
            }


            assembly.Write();
            EditorApplication.UnlockReloadAssemblies();




            Debug.Log("generated");
        } */
    }
}