using System;
using System.Collections.Generic;
using System.Linq;
using NeroWeNeed.Commons.Editor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static NeroWeNeed.ActionGraph.Editor.Graph.ActionGraphView;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public class SearchWindow : ScriptableObject, ISearchWindowProvider {

        public ActionGraphView graphView;
        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context) {
            var asset = ProjectUtility.GetSettings<ActionGraphGlobalSettings>();

            if (asset == null) {
                return null;
            }
            List<SearchTreeEntry> result = new List<SearchTreeEntry>();
            result.Add(new SearchTreeGroupEntry(new GUIContent("Nodes")));

            if (graphView.modules.Count > 1) {

                foreach (var module in graphView.modules) {
                    result.Add(new SearchTreeGroupEntry(new GUIContent(asset[module.asset.action].Name), 1));
                    CreateSearchTree(result, context, module, asset, 2);
                }
            }
            else if (graphView.modules.Count == 1) {
                CreateSearchTree(result, context, graphView.modules[0], asset, 1);
            }
            return result;
        }
        private void CreateSearchTree(List<SearchTreeEntry> treeEntries, SearchWindowContext context, ActionModule module, ActionGraphGlobalSettings asset, int depth) {
            var info = asset[module.asset.action];
            foreach (var node in info.nodes) {
                treeEntries.Add(new SearchTreeEntry(new GUIContent(node.Name))
                {
                    level = depth,
                    userData = new EntryData { actionType = new ActionType { guid = info.guid }, identifier = node.identifier }
                });
            }
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context) {
            if (searchTreeEntry.userData is EntryData data) {
                var guid = Guid.NewGuid().ToString("N");
                var settings = ProjectUtility.GetSettings<ActionGraphGlobalSettings>();
                var actionData = new ActionGraphModel.ActionNodeData
                {
                    actionType = data.actionType,
                    identifier = data.identifier,
                };
                graphView.model[guid] = actionData;
                graphView.AddElement(actionData.CreateNode(default, settings, new Rect(graphView.contentViewContainer.WorldToLocal(graphView.panel.visualTree.ChangeCoordinatesTo(graphView.panel.visualTree, context.screenMousePosition - graphView.window.position.position)), ActionGraphView.DefaultNodeSize), guid));
            }
            return true;
        }
        private struct EntryData {
            public ActionType actionType;
            public string identifier;

        }
    }
}