using System;
using System.Collections.Generic;
using System.Linq;
using NeroWeNeed.ActionGraph.Editor.Schema;
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
            var settings = ProjectUtility.GetProjectSettings<ActionGraphGlobalSettings>();

            if (settings == null) {
                return null;
            }
            List<SearchTreeEntry> result = new List<SearchTreeEntry>();
            result.Add(new SearchTreeGroupEntry(new GUIContent("Nodes")));
            var useFieldOps = graphView.model.context.modules.Any(module => settings.TryGetActionInfo(module.action, out var info) && info.useFieldOperations);
            if (graphView.model.context.modules.Count > 1 || useFieldOps) {
                foreach (var module in graphView.model.context.modules) {
                    result.Add(new SearchTreeGroupEntry(new GUIContent(settings[module.action].Name), 1));
                    CreateSearchTree(result, context, module, settings, 2);
                }
                if (useFieldOps) {
                    var fieldOps = ProjectUtility.GetOrCreateProjectAsset<FieldOperationSchema>();
                    var ops = new List<SearchTreeEntry>();
                    result.Add(new SearchTreeGroupEntry(new GUIContent("Operations"), 1));
                    foreach (var op in fieldOps.data) {
                        ops.Add(new SearchTreeEntry(new GUIContent(op.Value.Name))
                        {
                            level = 2,
                            userData = new EntryData { identifier = op.Value.identifier, fieldOp = true }
                        });
                    }
                    ops.Sort((entryA, entryB) => entryA.name.CompareTo(entryB.name));
                    result.AddRange(ops);
                }
            }
            else if (graphView.model.context.modules.Count == 1) {
                CreateSearchTree(result, context, graphView.model.context.modules[0], settings, 1);
            }
            return result;
        }

        private void CreateSearchTree(List<SearchTreeEntry> treeEntries, SearchWindowContext context, ActionModule module, ActionGraphGlobalSettings settings, int depth) {
            var info = settings[module.action];
            var entries = new List<SearchTreeEntry>();
            foreach (var action in settings.GetActions(module.action)) {
                entries.Add(new SearchTreeEntry(new GUIContent(action.Name))
                {
                    level = depth,
                    userData = new EntryData { actionType = module.action, identifier = action.identifier, fieldOp = false }
                });
            }
            entries.Sort((entryA, entryB) => entryA.name.CompareTo(entryB.name));
            treeEntries.AddRange(entries);
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context) {
            if (searchTreeEntry.userData is EntryData data) {
                var guid = Guid.NewGuid().ToString("N");
                var settings = ProjectUtility.GetProjectSettings<ActionGraphGlobalSettings>();
                if (data.fieldOp) {
                    var operations = ProjectUtility.GetProjectAsset<FieldOperationSchema>();
                    var nodeData = new FieldOperationNodeData
                    {
                        identifier = data.identifier,
                        subIdentifier = operations.data[data.identifier].GetDefaultSubIdentifier()
                    };
                    graphView.model[guid] = nodeData;
                    graphView.AddElement(nodeData.CreateNode(graphView, settings, new Rect(graphView.contentViewContainer.WorldToLocal(graphView.panel.visualTree.ChangeCoordinatesTo(graphView.panel.visualTree, context.screenMousePosition - graphView.window.position.position)), ActionGraphView.DefaultNodeSize), guid));
                    graphView.RefreshNodeConnections();
                }
                else {
                    var actionData = new ActionNodeData
                    {
                        actionId = data.actionType,
                        identifier = data.identifier,
                        subIdentifier = settings.GetAction(data.actionType, data.identifier).GetDefaultSubIdentifier()
                    };
                    graphView.model[guid] = actionData;
                    graphView.AddElement(actionData.CreateNode(graphView, settings, new Rect(graphView.contentViewContainer.WorldToLocal(graphView.panel.visualTree.ChangeCoordinatesTo(graphView.panel.visualTree, context.screenMousePosition - graphView.window.position.position)), ActionGraphView.DefaultNodeSize), guid));
                    graphView.RefreshNodeConnections();
                }

            }
            return true;
        }
        private struct EntryData {
            public bool fieldOp;
            public ActionId actionType;
            public string identifier;

        }
    }
}