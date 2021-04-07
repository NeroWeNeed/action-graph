using System;
using System.Linq;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons.Editor;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEditor;
using UnityEngine;
namespace NeroWeNeed.ActionGraph.Editor {
    public class ActionAssetPostprocessor : AssetPostprocessor {
        public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
            var settings = ProjectUtility.GetOrCreateProjectSettings<ActionGraphGlobalSettings>();
            var actionSchema = ProjectUtility.GetOrCreateProjectAsset<ActionSchema>();
            var fieldOperationSchema = ProjectUtility.GetOrCreateProjectAsset<FieldOperationSchema>();
            foreach (var path in importedAssets.Where(importedAsset => importedAsset.EndsWith($".{ActionGraphGlobalSettings.Extension}"))) {
                var importer = AssetImporter.GetAtPath(path);
                var userData = importer.userData;
                string artifactFile;
                if (string.IsNullOrWhiteSpace(userData)) {
                    artifactFile = settings.CreateArtifactPath();
                }
                else {
                    artifactFile = AssetDatabase.GUIDToAssetPath(userData);
                    if (string.IsNullOrEmpty(artifactFile)) {
                        artifactFile = settings.CreateArtifactPath();
                    }
                }
                var actionAsset = AssetDatabase.LoadAssetAtPath<ActionAsset>(path);
                var actionDefinitionAsset = ActionDefinitionAsset.Load(actionAsset.actionId);
                if (actionDefinitionAsset?.delegateType.IsCreated == true) {
                    var guid = (string)typeof(ActionGraphSerializer).GetMethod(nameof(ActionGraphSerializer.WriteArtifact), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .MakeGenericMethod(actionDefinitionAsset.delegateType)
                    .Invoke(null, new object[] {
                        artifactFile,actionSchema,fieldOperationSchema,actionAsset,actionDefinitionAsset
                    });
                    if (guid != importer.userData) {
                        importer.userData = guid;
                        importer.SaveAndReimport();
                    }
                }
                else {
                    AssetDatabase.DeleteAsset(artifactFile);
                }
            }
            foreach (var path in deletedAssets.Where(deletedAsset => deletedAsset.EndsWith($".{ActionGraphGlobalSettings.Extension}"))) {
                var userData = AssetImporter.GetAtPath(path).userData;
                if (!string.IsNullOrWhiteSpace(userData)) {
                    var artifactPath = AssetDatabase.GUIDToAssetPath(userData);
                    if (!string.IsNullOrEmpty(artifactPath)) {
                        AssetDatabase.DeleteAsset(artifactPath);
                    }
                }
            }
            AssetDatabase.Refresh();
        }
    }
}