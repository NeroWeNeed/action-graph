using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public enum PortUpdateEventType {
        Connected, Disconnected
    }
    public class PortUpdateEvent : EventBase<PortUpdateEvent> {
        public Port portA;
        public Port portB;
        public PortUpdateEventType type;

        public static PortUpdateEvent GetPooled(Port portA,Port portB,PortUpdateEventType type, IEventHandler target) {
            var evt = EventBase<PortUpdateEvent>.GetPooled();
            evt.portA = portA;
            evt.portB = portB;
            evt.type = type;
            evt.bubbles = true;
            evt.target = target;
            return evt;
        }
    }


}