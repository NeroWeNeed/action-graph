using System;
using System.IO;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditorInternal;
using UnityEngine;
using static NeroWeNeed.ActionGraph.Editor.ActionGraphGlobalSettings;

namespace NeroWeNeed.ActionGraph.Editor {
    [ScriptedImporter(1, "action")]
    public class ActionAssetImporter : ScriptedImporter {
        public override void OnImportAsset(AssetImportContext ctx) {
            var json = File.ReadAllText(ctx.assetPath);
            var asset = ScriptableObject.CreateInstance<ActionAsset>();
            var settings = ProjectUtility.GetOrCreateProjectSettings<ActionGraphGlobalSettings>();
            var jsonSettings = JsonConvert.DefaultSettings.Invoke();
            jsonSettings.TypeNameHandling = TypeNameHandling.All;
            var model = JsonConvert.DeserializeObject<ActionAssetModel>(json, jsonSettings);
            if (model.id.IsCreated) {
                AssetDatabase.SetLabels(asset, new string[] { $"Action:{model.id.ToString()}" });
            }
            asset.actionId = model.id;
            if (string.IsNullOrWhiteSpace(userData) || AssetDatabase.GUIDToAssetPath(userData) == null) {
                var artifactPath = settings.CreateArtifactPath();

                AssetDatabase.CreateAsset(new TextAsset(), artifactPath);
                userData = AssetDatabase.GUIDFromAssetPath(artifactPath).ToString();
                Debug.Log(artifactPath);
                Debug.Log(userData);
            }
            ctx.AddObjectToAsset("Action", asset, ActionAssetEditor.AssetIcon);
            ctx.SetMainObject(asset);

        }
    }
}