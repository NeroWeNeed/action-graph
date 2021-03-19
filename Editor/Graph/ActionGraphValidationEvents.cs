using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    internal class ActionGraphValidationRequestEvent : EventBase<ActionGraphValidationRequestEvent> {
        public static ActionGraphValidationRequestEvent GetPooled(IEventHandler target) {
            var evt = EventBase<ActionGraphValidationRequestEvent>.GetPooled();
            evt.bubbles = false;
            evt.tricklesDown = false;
            evt.target = target;
            return evt;
        }
    }
    public class ActionGraphValidationUpdateEvent : EventBase<ActionGraphValidationUpdateEvent> {
        public ActionGraphView graphView;
        public bool isValid;
        public static ActionGraphValidationUpdateEvent GetPooled(ActionGraphView graphView,bool isValid,IEventHandler target) {
            var evt = EventBase<ActionGraphValidationUpdateEvent>.GetPooled();
            evt.bubbles = true;
            evt.target = target;
            evt.graphView = graphView;
            evt.isValid = isValid;
            return evt;
        }
    }
}