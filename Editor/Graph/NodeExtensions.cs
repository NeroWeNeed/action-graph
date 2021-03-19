using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public static class NodeExtensions {
        public const string NodeErrorNotificationClassName = "node-notification--error";
        public const string NodeNotificationClassName = "node-notification";
        public const string NodeNotificationContainerClassName = "node-notification-container";
        public const string NodeInfoNotificationClassName = "node-notification--info";
        public static void AddError(this Node node, string message) {
            var item = GetNotificationItem(node, NotificationType.Error);
            item.badgeText = message;
        }
        public static void AddInfo(this Node node, string message) {
            var item = GetNotificationItem(node, NotificationType.Info);
            item.badgeText = message;
        }
        private static IconBadge GetNotificationItem(Node node, NotificationType type) {
            string className;
            switch (type) {
                case NotificationType.Error:
                    className = NodeErrorNotificationClassName;
                    break;
                case NotificationType.Info:
                    className = NodeInfoNotificationClassName;
                    break;
                default:
                    return null;
            }
            var item = node.Q<IconBadge>(null, className);
            if (item == null) {
                node.ClearNotifications();
                switch (type) {
                    case NotificationType.Error:
                        item = IconBadge.CreateError(string.Empty);
                        break;
                    case NotificationType.Info:
                        item = IconBadge.CreateComment(string.Empty);
                        break;
                }
                item.AddToClassList(NodeNotificationClassName);
                item.AddToClassList(className);
                node.Add(item);
                item.AttachTo(node.titleContainer, UnityEngine.SpriteAlignment.RightCenter);
            }
            return item;
        }
        public static void ClearNotifications(this Node node) {
            foreach (var item in node.Query<VisualElement>(null, NodeNotificationClassName).ToList()) {
                item.RemoveFromHierarchy();
            }
        }
        private enum NotificationType {
            Error, Info
        }
    }
}