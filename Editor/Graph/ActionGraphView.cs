using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public class ActionGraphView : GraphView {
        public const string FieldPortClassName = "action-graph-field-port";
        public const string FieldOperationOutputPortClassName = "action-graph-field-operation-output-port";
        public const string NodePortClassName = "action-graph-node-port";
        public const string CollectablePortClassName = "action-graph-collectable-port";
        public const string NodeInputPortClassName = "action-graph-input-node-port";
        public const string NodeOutputPortClassName = "action-graph-output-node-port";
        public const string OutputPortClassName = "action-graph-output-port";
        public const string NodeConnectionPortClassName = "action-graph-node-connection-port";
        public const string MasterNodePortClassName = "action-graph-master-node-port";
        public const string SilentPortClassName = "action-graph-silent-port";
        public const string ElementContainerClassName = "action-graph-element-container";
        public const string SilentEdgeClassName = "action-graph-silent-edge";
        public const string ContainerClassName = "action-graph-field-container";
        public const string FieldClassName = "action-graph-field";
        public const string VariableIconPath = "Packages/github.neroweneed.action-graph/Editor/Resources/VariableIcon.png";
        public const string ActionNodeClassName = "action-graph-node--action";
        public const string FieldOperationNodeClassName = "action-graph-node--field-operation";
        public const string VariableNodeClassName = "action-graph-node--variable";
        public const string MasterNodeClassName = "action-graph-node--master";

        private Texture variableIcon;
        public Texture VariableIcon { get => variableIcon ?? AssetDatabase.LoadAssetAtPath<Texture>(VariableIconPath); }
        public ActionGraphModel model = new ActionGraphModel();
        private SearchWindow searchWindow;
        public static readonly Vector2 DefaultNodeSize = new Vector2(100, 100);
        private MasterNode masterNode;
        private Inspector inspector;
        internal EditorWindow window;
        private Vector2 lastContextualMousePosition;
        private SimpleTypeCache<ActionValidationRule> actionValidationRuleCache = new SimpleTypeCache<ActionValidationRule>();
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
            this.RegisterCallback<MouseDownEvent>(evt =>
            {
                if ((evt.pressedButtons & 2) != 0) {

                    lastContextualMousePosition = evt.mousePosition;
                }
            });
            SetupSearchWindow();
            SetupInspector();
            this.graphViewChanged += FirePortChangeEvents;
            this.graphViewChanged += UpdateSilentElements;
            this.graphViewChanged += UpdateValidationState;
            this.serializeGraphElements += SerializeElements;
            this.unserializeAndPaste += DeserializeElements;
            masterNode = new MasterNode();
            this.AddElement(masterNode);
            this.RegisterCallback<ActionGraphValidationRequestEvent>(evt =>
            {
                var self = (ActionGraphView)evt.target;
                var result = self.IsValid();
                using var resultEvent = ActionGraphValidationUpdateEvent.GetPooled(self, result, evt.target);
                self.SendEvent(resultEvent);
            });
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
                            var variableData = new VariableNodeData
                            {
                                path = info.Item2,
                                actionId = new ActionId { guid = info.Item1 }
                            };
                            var guid = Guid.NewGuid().ToString("N");
                            var settings = ProjectUtility.GetProjectSettings<ActionGraphGlobalSettings>();
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
            this.DeleteElements(this.masterNode.Query<Port>(null, MasterNodePortClassName).ToList());
        }
        public bool IsValid() {
            var moduleNodeCollections = new Dictionary<int, NodeCollection>();
            var settings = ProjectUtility.GetProjectSettings<ActionGraphGlobalSettings>();
            masterNode.inputContainer.Query<Port>(null, MasterNodePortClassName).ForEach(port =>
            {
                var moduleIndex = this.model.context.modules.FindIndex(m => m.guid == port.viewDataKey);
                if (moduleIndex < 0)
                    return;
                moduleNodeCollections[moduleIndex] = new NodeCollection(port);
            });
            return IsValid(settings, moduleNodeCollections);
        }
        private bool IsValid(ActionGraphGlobalSettings settings, Dictionary<int, NodeCollection> moduleNodeCollections) {
            foreach (var node in nodes) {
                node.ClearNotifications();
            }
            foreach (var moduleNodeCollection in moduleNodeCollections) {
                var info = model.context.modules[moduleNodeCollection.Key].definitionAsset;
                if (info.validatorType.IsCreated && !actionValidationRuleCache[info.validatorType.Value].IsValid(this, moduleNodeCollection.Value)) {
                    return false;
                }
            }
            return true;
        }
        public void LoadModule(ActionModule module, bool clear = true) {
            if (clear) {
                ClearGraph();
            }
            if (module.asset == null)
                return;
            var settings = ProjectUtility.GetProjectSettings<ActionGraphGlobalSettings>();
            LoadModule(module, settings);
            model.context.Container = null;
            RefreshNodeConnections();
            inspector.RefreshVariables();
            inspector.RefreshProperties();
            FrameAll();
        }
        public void RefreshNodeConnections() {
            var masterNodeGuid = Guid.Empty.ToString("N");
            foreach (var containerNodePort in ports.Where(p => p.ClassListContains(NodeConnectionPortClassName) && !p.ClassListContains(SilentPortClassName))) {
                var containerNode = containerNodePort.node;
                var containerNodeGuid = containerNode.viewDataKey;
                var containerNodeData = this.model[containerNodeGuid];
                string targetNodeGuid;
                if (containerNodePort.ClassListContains(OutputPortClassName) && containerNodeData is INodeOutput nodeOutput) {
                    targetNodeGuid = nodeOutput.Next;
                }
                else if (containerNodeData is IPropertyContainer propertyContainer && propertyContainer.Properties.TryGetValue(containerNodePort.viewDataKey, out object property) && property is INodeReference nodeReference) {

                    targetNodeGuid = nodeReference.Get();
                }
                else {
                    continue;
                }


                if (string.IsNullOrEmpty(targetNodeGuid)) {
                    containerNodePort.DisconnectAll();
                }
                else {
                    if (containerNodePort.connected) {
                        var connection = containerNodePort.connections.First();
                        if (connection.input?.node?.viewDataKey == targetNodeGuid) {
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
                    else if (targetNode is MasterNode && containerNodeData is ActionNodeData actionNodeData) {
                        var targetNodePort = targetNode.inputContainer.Query<Port>(null, MasterNodePortClassName).Where(p => p.viewDataKey == actionNodeData.moduleHint).First();
                        if (targetNodePort == null) {
                            containerNodePort.DisconnectAll();
                        }
                        else {
                            this.AddElement(containerNodePort.ConnectTo(targetNodePort));
                        }
                    }
                    else if (containerNodeData is IPropertyConnector propertyTargetingNodeData) {
                        var targetNodePort = targetNode.Query<Port>(null, FieldPortClassName).Where(p => p.viewDataKey == propertyTargetingNodeData.PropertyHint).First();

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
                if (container == null)
                    continue;
                if (fieldPort.connected) {
                    container.visible = fieldPort.connections.First().ClassListContains(SilentEdgeClassName);
                }
                else {
                    var edge = fieldPort.ConnectTo(container.Q<Port>(null, SilentPortClassName));
                    edge.capabilities ^= Capabilities.Selectable;
                    edge.AddToClassList(ActionGraphView.SilentEdgeClassName);
                    AddElement(edge);
                    container.visible = true;
                }
            }
            using var evt = ActionGraphValidationRequestEvent.GetPooled(this);
            SendEvent(evt);
        }
        public void LoadModules(IEnumerable<ActionModule> modules, ScriptableObject container = null, bool clear = true) {
            if (clear) {
                ClearGraph();
            }
            var settings = ProjectUtility.GetProjectSettings<ActionGraphGlobalSettings>();
            foreach (var module in modules) {
                if (module.asset == null)
                    continue;
                LoadModule(module, settings);
            }
            model.context.Container = container;
            inspector.RefreshVariables();
            inspector.RefreshProperties();
            RefreshNodeConnections();
            FrameAll();
        }

        private void LoadModule(ActionModule module, ActionGraphGlobalSettings settings) {

            this.model.context.modules.Add(module);
            masterNode.AddModule(module);
            var model = module.asset.CreateModel();
            if (!this.model.actionInfo.ContainsKey(module.action.guid)) {
                var actionInfo = module.definitionAsset;
                var variableType = actionInfo.variableType;
                var variables = new Dictionary<string, ActionGraphModel.VariableInfo>();
                if (variableType.IsCreated) {

                    variableType.Value.Decompose((type, fieldInfo, parent, path, parentPath, options) =>
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

                this.model.actionInfo[module.action.guid] = new ActionGraphModel.ActionInfo
                {
                    type = actionInfo.delegateType.Value,
                    variables = variables
                };
            }
            foreach (var node in model.nodes) {
                try {
                    node.Value.Data.moduleHint = module.guid;
                    this.model.nodes[node.Key] = node.Value.Data;
                    this.AddElement(node.Value.CreateNode(this, settings, node.Value.layout, node.Key));
                }
                catch (Exception) {
                    this.model.nodes.Remove(node.Key);
                }

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
        private GraphViewChange UpdateValidationState(GraphViewChange change) {
            using var evt = ActionGraphValidationRequestEvent.GetPooled(this);
            SendEvent(evt);
            return change;
        }
        private GraphViewChange UpdateSilentElements(GraphViewChange change) {
            if (change.elementsToRemove != null) {
                foreach (var port in change.elementsToRemove.SelectMany(element => element.Query<Port>().Where(port => port.connected).ToList()).ToList()) {
                    foreach (var connection in port.connections) {
                        if (connection.ClassListContains(SilentEdgeClassName)) {
                            connection.RemoveFromHierarchy();


                        }
                    }
                }
            }
            return change;
        }
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) {
            var containerNode = startPort.GetFirstAncestorOfType<Node>();
            return this.ports.Where(port => (port.GetFirstAncestorOfType<Node>() != containerNode) && PortTypeMatches(port.portType, startPort.portType) && port.direction != startPort.direction && !port.ClassListContains(SilentPortClassName)).ToList();
        }
        private static bool PortTypeMatches(Type a, Type b) {
            Type fieldTypeA = a.GetGenericInterface(typeof(IFieldCompatible<>));
            Type fieldTypeB = b.GetGenericInterface(typeof(IFieldCompatible<>));
            return a == b || (fieldTypeA != null && fieldTypeB != null && fieldTypeA == fieldTypeB);
        }
        public void SaveModules() {
            var settings = ProjectUtility.GetProjectSettings<ActionGraphGlobalSettings>();
            if (settings == null)
                return;
            var moduleNodeCollections = new Dictionary<int, NodeCollection>();
            masterNode.inputContainer.Query<Port>(null, MasterNodePortClassName).ForEach(port =>
            {
                var moduleIndex = this.model.context.modules.FindIndex(m => m.guid == port.viewDataKey);
                if (moduleIndex < 0)
                    return;
                moduleNodeCollections[moduleIndex] = new NodeCollection(port);
            });
            if (!IsValid(settings, moduleNodeCollections)) {
                return;
            }
            foreach (var moduleNodeSet in moduleNodeCollections) {
                Save(moduleNodeSet.Value, model.context.modules[moduleNodeSet.Key], settings);
            }
        }
        public void Save(NodeCollection nodes, ActionModule module, ActionGraphGlobalSettings settings) {
            var model = new ActionAssetModel
            {
                id = module.definitionAsset.id,
                masterNodeLayout = masterNode.GetPosition(),
                nodes = nodes.ToDictionary(info => info.guid, info =>
                {
                    var data = this.model[info.guid];
                    return data.ToModelNode(this.GetNodeByGuid(info.guid).GetPosition());

                })
            };
            var jsonSettings = JsonConvert.DefaultSettings.Invoke();

            jsonSettings.TypeNameHandling = TypeNameHandling.All;
            var path = AssetDatabase.GetAssetPath(module.asset);
            if (string.IsNullOrEmpty(path)) {
                path = EditorUtility.SaveFilePanelInProject($"Save Action Asset {module.name} as...", module.definitionAsset.Name, ActionGraphGlobalSettings.Extension, string.Empty);
                if (string.IsNullOrEmpty(path)) {
                    return;
                }
            }
            File.WriteAllText(path, JsonConvert.SerializeObject(model, jsonSettings));
            AssetDatabase.ImportAsset(path);
        }
        private string SerializeElements(IEnumerable<GraphElement> elements) {
            var data = elements.Where(element => this.model.nodes.ContainsKey(element.viewDataKey)).ToDictionary(element => element.viewDataKey, element => this.model[element.viewDataKey].ToModelNode(element.GetPosition()));
            var jsonSettings = JsonConvert.DefaultSettings.Invoke();
            jsonSettings.TypeNameHandling = TypeNameHandling.All;
            return JsonConvert.SerializeObject(data);
        }
        private void DeserializeElements(string operationName, string data) {
            var elements = JsonConvert.DeserializeObject<Dictionary<string, ActionAssetModel.Node>>(data);
            var guidMapping = elements.Keys.ToDictionary(key => key, _ => Guid.NewGuid().ToString("N"));
            var settings = ProjectUtility.GetProjectSettings<ActionGraphGlobalSettings>();
            elements.ToDictionary(element => guidMapping[element.Key], element => element);
            Vector2 topLeft = Vector2.zero;
            bool isTopLeftSet = false;
            foreach (var element in elements) {
                element.Value.Data.RemapGuid(guidMapping);
                this.model.nodes[guidMapping[element.Key]] = element.Value.Data;
                if (isTopLeftSet) {
                    topLeft = element.Value.layout.position;
                    isTopLeftSet = true;
                }
                else {
                    topLeft.x = topLeft.x < element.Value.layout.x ? topLeft.x : element.Value.layout.x;
                    topLeft.y = topLeft.y < element.Value.layout.y ? topLeft.y : element.Value.layout.y;
                }
            }
            foreach (var element in elements) {
                element.Value.layout.position -= topLeft;
                element.Value.layout.position += contentViewContainer.WorldToLocal(panel.visualTree.ChangeCoordinatesTo(panel.visualTree, lastContextualMousePosition - window.position.position));
                AddElement(element.Value.CreateNode(this, settings, element.Value.layout, guidMapping[element.Key]));
            }
            RefreshNodeConnections();
        }
        public new class UxmlFactory : UxmlFactory<ActionGraphView, ActionGraphView.UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }
        public static Type CreateActionPortType(Type action) => typeof(ActionPort<>).MakeGenericType(action);
        public static Type CreateFieldPortType(Type action, Type field) => typeof(FieldPort<,>).MakeGenericType(action, field);
        public static Type CreateFieldOperationPortType(Type field) => typeof(FieldOperationPort<>).MakeGenericType(field);

    }
}