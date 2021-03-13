using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using Mono.Cecil;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using static NeroWeNeed.ActionGraph.Editor.ActionGraphGlobalSettings;

namespace NeroWeNeed.ActionGraph.Editor {
    public static class ActionGraphUtility {

        public static ActionModel CreateModel(this ActionAsset asset) {
            var jsonSettings = JsonConvert.DefaultSettings.Invoke();
            jsonSettings.TypeNameHandling = TypeNameHandling.All;
            return JsonConvert.DeserializeObject<ActionModel>(asset.json.text, jsonSettings);

        }
        /*         public static ActionAsset ToAsset(this ActionModel model) {
                    var asset = ScriptableObject.CreateInstance<ActionAsset>();
                    asset.action.guid = model.action;
                    //asset.masterNodeLayout = model.masterNodeLayout;
                    asset.nodes = model.nodes.Select(n => new ActionAsset.Node
                    {
                        id = n.Key,
                        nextId = n.Value.next,
                        nodeIdentifier = n.Value.identifier,
                        layout = n.Value.layout,
                        properties = n.Value.properties.Select(p => new ActionAsset.Node.Property { path = p.Key, value = p.Value }).ToList()
                    }).ToList();
                    return asset;
                }


                public static void UpdateAsset(this ActionAsset asset, ActionModel model) {
                    asset.action.guid = model.action;
                    //asset.masterNodeLayout = model.masterNodeLayout;
                    asset.nodes = model.nodes.Select(n => new ActionAsset.Node
                    {
                        id = n.Key,
                        nextId = n.Value.next,
                        nodeIdentifier = n.Value.identifier,
                        layout = n.Value.layout,
                        properties = n.Value.properties.Select(p => new ActionAsset.Node.Property { path = p.Key, value = p.Value }).ToList()
                    }).ToList();
                }
         */
    }
}