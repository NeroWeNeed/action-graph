namespace NeroWeNeed.ActionGraph.Editor.Graph {
    internal interface IFieldCompatible<TField> { }
    internal sealed class ActionPort<TAction> { }
    internal sealed class FieldPort<TAction, TType> : IFieldCompatible<TType> { }
    internal sealed class FieldOperationPort<TType> : IFieldCompatible<TType> { }

}