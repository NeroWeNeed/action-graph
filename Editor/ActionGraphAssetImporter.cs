using System.IO;
using NeroWeNeed.Commons;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditorInternal;
using UnityEngine;

namespace NeroWeNeed.ActionGraph.Editor {
    [ScriptedImporter(1, "action")]
    public class ActionGraphAssetImporter : ScriptedImporter {
        public override void OnImportAsset(AssetImportContext ctx) {
            var json = File.ReadAllText(ctx.assetPath);
            var asset = ScriptableObject.CreateInstance<ActionAsset>();
            var model = JsonConvert.DeserializeObject<ActionModel>(json);
            asset.action = new ActionType { guid = model.action };
            var textAsset = new TextAsset(json);
            textAsset.name = "Model";
            asset.json = textAsset;
            ctx.AddObjectToAsset("Action", asset);
            ctx.AddObjectToAsset("Model", textAsset);
            ctx.SetMainObject(asset);

        }
    }
}