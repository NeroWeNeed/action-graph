using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public partial class ActionGraphView : GraphView {



        public const string FieldPortClassName = "action-graph-field-port";
        public List<ActionModule> modules = new List<ActionModule>();
        public ActionGraphModel model = new ActionGraphModel();
        private SearchWindow searchWindow;
        public static readonly Vector2 DefaultNodeSize = new Vector2(100, 100);
        private MasterNode masterNode;
        private Inspector inspector;
        internal EditorWindow window;
        public ActionGraphView() {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new ClickSelector());
            this.AddManipulator(new RectangleSelector());
            this.contentViewContainer.RegisterCallback<GeometryChangedEvent>((evt) =>
            {
                if (evt.oldRect.width == 0 && evt.oldRect.height == 0)
                    FrameAll();
            });
            SetupSearchWindow();
            SetupInspector();
            this.graphViewChanged += FirePortChangeEvents;
            masterNode = new MasterNode();
            this.AddElement(masterNode);
        }
        private void SetupSearchWindow() {
            searchWindow = ScriptableObject.CreateInstance<SearchWindow>();
            nodeCreationRequest = (context) =>
            {
                searchWindow.graphView = this;
                UnityEditor.Experimental.GraphView.SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), searchWindow);
            };
        }
        private void SetupInspector() {
            inspector = new Inspector(this);
            this.Add(inspector);
            this.RegisterCallback<DragUpdatedEvent>((evt) =>
            {
                var z = DragAndDrop.GetGenericData("DragSelection");
                if (z is List<ISelectable>) {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                }
            });
            this.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (DragAndDrop.GetGenericData("DragSelection") is List<ISelectable> selectables) {
                    Vector2 offset = default;
                    var pos = evt.mousePosition;
                    foreach (var selectable in selectables) {
                        if (selectable is BlackboardField blackboardField && blackboardField.userData is ValueTuple<string, string> info) {
                            var variableData = new ActionGraphModel.VariableNodeData
                            {
                                path = info.Item2,
                                actionType = new ActionType { guid = info.Item1 }
                            };
                            var guid = Guid.NewGuid().ToString("N");
                            var settings = ProjectUtility.GetSettings<ActionGraphGlobalSettings>();
                            model[guid] = variableData;
                            AddElement(variableData.CreateNode(this, settings, new Rect(contentViewContainer.WorldToLocal(contentViewContainer.parent.ChangeCoordinatesTo(contentViewContainer.parent, evt.mousePosition)) + offset, DefaultNodeSize), guid));
                            offset.y += blackboardField.worldBound.height + 2;
                        }
                    }
                }
            });
        }
        public void ClearGraph() {
            this.DeleteElements(this.graphElements.Where(element => !(element is MasterNode)));
            this.model.Clear();
            this.modules.Clear();
            this.DeleteElements(this.masterNode.Query<Port>(null, MasterNodePortClassName).ToList());
        }
        public void LoadModule(ActionModule module, bool clear = true) {
            if (clear) {
                ClearGraph();
            }
            if (module.asset == null)
                return;
            var settings = ProjectUtility.GetSettings<ActionGraphGlobalSettings>();
            LoadModule(module, settings);
            RefreshNodeConnections();
            inspector.RefreshVariables();
            FrameAll();
        }

        public void RefreshNodeConnections() {
            var masterNodeGuid = Guid.Empty.ToString("N");
            foreach (var containerNodePort in ports.Where(p => p.ClassListContains(OutputPortClassName) && !p.ClassListContains(SilentPortClassName))) {
                var containerNode = containerNodePort.node;
                var containerNodeGuid = containerNode.viewDataKey;
                var containerNodeData = this.model[containerNodeGuid];
                var targetNodeGuid = containerNodeData.next;
                var action = model[containerNodeGuid].actionType;
                if (string.IsNullOrEmpty(targetNodeGuid)) {
                    containerNodePort.DisconnectAll();
                }
                else {
                    if (containerNodePort.connected) {
                        var connection = containerNodePort.connections.First();
                        if (connection.input.node.viewDataKey == targetNodeGuid) {
                            continue;
                        }
                        else {
                            containerNodePort.DisconnectAll();
                        }
                    }

                    var targetNode = this.GetNodeByGuid(targetNodeGuid);
                    if (targetNode == null) {
                        containerNodePort.DisconnectAll();
                    }
                    else if (targetNode is MasterNode && containerNodeData is ActionGraphModel.ActionNodeData actionNodeData) {
                        var targetNodePort = targetNode.inputContainer.Query<Port>(null, MasterNodePortClassName).Where(p => p.viewDataKey == actionNodeData.moduleHint).First();
                        if (targetNodePort == null) {
                            containerNodePort.DisconnectAll();
                        }
                        else {
                            this.AddElement(containerNodePort.ConnectTo(targetNodePort));
                        }
                    }
                    else if (containerNodeData is ActionGraphModel.PropertyTargetingNodeData propertyTargetingNodeData) {
                        var targetNodePort = targetNode.Query<Port>(null, FieldPortClassName).Where(p => p.viewDataKey == propertyTargetingNodeData.propertyHint).First();
                        if (targetNodePort == null) {
                            containerNodePort.DisconnectAll();
                        }
                        else {
                            this.AddElement(containerNodePort.ConnectTo(targetNodePort));
                        }
                    }
                    else {
                        var targetNodePort = targetNode.inputContainer.Q<Port>(null, NodeInputPortClassName);
                        if (targetNodePort == null) {
                            containerNodePort.DisconnectAll();
                        }
                        else {
                            this.AddElement(containerNodePort.ConnectTo(targetNodePort));
                        }
                    }




                }

            }

            foreach (var fieldPort in ports.Where(p => p.ClassListContains(FieldPortClassName))) {
                var container = fieldPort.node.Query<VisualElement>(null, ElementContainerClassName).Where(e => e.viewDataKey == fieldPort.viewDataKey).First();
                if (fieldPort.connected) {
                    if (fieldPort.connections.First().ClassListContains(SilentEdgeClassName)) {
                        container.visible = true;
                    }
                    else {
                        container.visible = false;
                    }

                }
                else {
                    var edge = fieldPort.ConnectTo(container.Q<Port>(null,SilentPortClassName));
                    edge.capabilities ^= Capabilities.Selectable;
                    edge.AddToClassList(ActionGraphView.SilentEdgeClassName);
                    AddElement(edge);
                    container.visible = true;
                }
            }
        }

        public void LoadModules(IEnumerable<ActionModule> modules, bool clear = true) {
            if (clear) {
                ClearGraph();
            }
            var settings = ProjectUtility.GetSettings<ActionGraphGlobalSettings>();
            foreach (var module in modules) {
                if (module.asset == null)
                    continue;
                LoadModule(module, settings);
            }
            inspector.RefreshVariables();
            RefreshNodeConnections();
            FrameAll();
        }
        private void LoadModule(ActionModule module, ActionGraphGlobalSettings settings) {

            this.modules.Add(module);
            masterNode.AddModule(module);
            var model = module.asset.CreateModel();
            if (!this.model.actionInfo.ContainsKey(module.asset.action.guid)) {
                var actionInfo = settings[module.asset.action];
                var variableType = actionInfo.variableType;
                var variables = new Dictionary<string, ActionGraphModel.VariableInfo>();
                if (variableType.IsCreated) {

                    variableType.Value.Decompose((type, fieldInfo, path, options) =>
                    {
                        variables[path] = new ActionGraphModel.VariableInfo
                        {
                            path = path,
                            type = type
                        };
                        return false;
                    }, new TypeDecompositionOptions
                    {
                        exploreChildren = true
                    });
                }

                this.model.actionInfo[module.asset.action.guid] = new ActionGraphModel.ActionInfo
                {
                    type = actionInfo.type.Value,
                    variables = variables
                };
            }
            foreach (var node in model.nodes) {
                node.Value.Data.moduleHint = module.guid;
                this.model.nodes[node.Key] = node.Value.Data;
                this.AddElement(node.Value.CreateNode(this, settings, node.Value.layout, node.Key));
            }


        }


        private GraphViewChange FirePortChangeEvents(GraphViewChange change) {
            var edgesToRemove = change.elementsToRemove?.OfType<Edge>();
            if (edgesToRemove != null) {
                foreach (var edge in edgesToRemove) {
                    if (edge.input != null) {
                        using var evtInput = PortUpdateEvent.GetPooled(edge.input, edge.output, PortUpdateEventType.Disconnected, edge.input); edge.input.SendEvent(evtInput);
                    }
                    if (edge.output != null) {
                        using var evtOutput = PortUpdateEvent.GetPooled(edge.input, edge.output, PortUpdateEventType.Disconnected, edge.output); edge.output.SendEvent(evtOutput);
                    }
                }
            }
            change.edgesToCreate?.ForEach(edge =>
            {
                if (edge.input != null) {
                    using var evtInput = PortUpdateEvent.GetPooled(edge.input, edge.output, PortUpdateEventType.Connected, edge.input); edge.input.SendEvent(evtInput);
                }
                if (edge.output != null) {
                    using var evtOutput = PortUpdateEvent.GetPooled(edge.input, edge.output, PortUpdateEventType.Connected, edge.output); edge.output.SendEvent(evtOutput);
                }
            });

            return change;
        }
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) {
            var containerNode = startPort.GetFirstAncestorOfType<Node>();
            return this.ports.Where(port => (port.GetFirstAncestorOfType<Node>() != containerNode) && port.portType == startPort.portType && port.direction != startPort.direction && !port.ClassListContains(SilentPortClassName)).ToList();
        }
        public void Save() {
            var settings = ProjectUtility.GetSettings<ActionGraphGlobalSettings>();
            if (settings == null)
                return;
            masterNode.inputContainer.Query<Port>(null, MasterNodePortClassName).ForEach(port =>
            {
                var moduleIndex = this.modules.FindIndex(m => m.guid == port.viewDataKey);
                if (moduleIndex < 0)
                    return;
                Save(port, modules[moduleIndex], settings);

            });
        }

        public void Save(Port start, ActionModule module, ActionGraphGlobalSettings settings) {
            var nodes = CollectNodes(start);
            var model = new ActionModel
            {
                action = settings[module.asset.action].guid,
                masterNodeLayout = masterNode.GetPosition(),
                nodes = nodes.ToDictionary(guid => guid, guid =>
                {
                    var data = this.model[guid];
                    return data.ToModelNode(this.GetNodeByGuid(guid).GetPosition());

                })
            };
            var jsonSettings = JsonConvert.DefaultSettings.Invoke();
            jsonSettings.TypeNameHandling = TypeNameHandling.All;
            var path = AssetDatabase.GetAssetPath(module.asset);
            if (string.IsNullOrEmpty(path)) {
                //TODO: Save File Prompt
            }
            else {
                File.WriteAllText(path, JsonConvert.SerializeObject(model, jsonSettings));
            }
            AssetDatabase.ImportAsset(path);

        }
        public static HashSet<string> CollectNodes(Port port) {
            var guids = new HashSet<string>();
            CollectNodes(port, guids);
            return guids;
        }
        private static void CollectNodes(Port port, HashSet<string> guids) {
            foreach (var connection in port.connections) {
                var node = connection.output.node;
                if (guids.Add(node.viewDataKey)) {
                    foreach (var inputPort in node.Query<Port>(null, ActionGraphView.CollectablePortClassName).ToList()) {
                        CollectNodes(inputPort, guids);
                    }
                }
            }
        }


        public new class UxmlFactory : UxmlFactory<ActionGraphView, ActionGraphView.UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        public static Type CreateActionPortType(Type action) => typeof(ActionPort<>).MakeGenericType(action);
        public static Type CreateFieldPortType(Type action, Type field) => typeof(FieldPort<,>).MakeGenericType(action, field);

        public static Type CreateVariablePortType(Type variable, Type action) => typeof(VariablePort<,>).MakeGenericType(variable, action);

    }
}