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
            var actionSchema = ProjectUtility.GetProjectAsset<ActionSchema>();
            List<SearchTreeEntry> result = new List<SearchTreeEntry>();

            result.Add(new SearchTreeGroupEntry(new GUIContent("Nodes")));
            var useFieldOps = graphView.model.context.modules.Any(module => module.definitionAsset.useFieldOperations);
            if (graphView.model.context.modules.Count > 1 || useFieldOps) {
                foreach (var module in graphView.model.context.modules) {
                    result.Add(new SearchTreeGroupEntry(new GUIContent(module.definitionAsset.Name), 1));
                    CreateSearchTree(result, module, actionSchema, 2);
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
                CreateSearchTree(result, graphView.model.context.modules[0], actionSchema, 1);
            }
            return result;
        }

        private void CreateSearchTree(List<SearchTreeEntry> treeEntries, ActionModule module, ActionSchema schema, int depth) {

            var entries = new List<SearchTreeEntry>();
            foreach (var action in schema[module.definitionAsset]) {
                entries.Add(new SearchTreeEntry(new GUIContent(action.Name))
                {
                    level = depth,
                    userData = new EntryData { actionId = module.action, identifier = action.identifier, fieldOp = false, actionType = module.definitionAsset.delegateType }
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
                    var actionSchema = ProjectUtility.GetProjectAsset<ActionSchema>();
                    var actionData = new ActionNodeData
                    {
                        actionId = data.actionId,
                        identifier = data.identifier,
                        subIdentifier = actionSchema[data.actionType, data.identifier].DefaultSubIdentifier
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
            public ActionId actionId;
            public Type actionType;
            public string identifier;

        }
    }
}