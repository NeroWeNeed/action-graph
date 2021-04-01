# Action-Graph
Action-Graph is a Unity Editor Extension tool designed to create state-machine styled assets for pluggable behaviour in Unity DOTS. This tool was developed for internal purposes and there are still things that need to be fixed, so use at your own risk.

## Motivation
Action-Graph was designed as an aid for programmers instead of a replacement. Graph based logic is inherently unintuitive to write, but annoying to deal with in a visual scripting environment. This extension was designed as a middle ground, allowing all the nodes necessary for your use case to be easy to write in code, while being easy to organize in the graph editor.

## Features
- Define Actions with delegates and the ActionDefinition Asset.
    - Almost Any parameter configuration is allowed as long as it falls under the [DLLImport and Internal Calls](https://docs.unity3d.com/Packages/com.unity.burst@1.5/manual/docs/CSharpLanguageSupport_BurstIntrinsics.html#dllimport-and-internal-calls) constraints. 
    - Define extra parameters with user-defined components tagged with ActionArgumentAttribute. 
    - Define extra parameters to take singleton arguments by tagging the parameter with the SingletonArgumentAttribute.
- Node-based editor for combining segments of code.
    - Automatically finds relevant nodes through Burstable methods tagged with ActionAttribute.
    - Variable support
    - Customizable Field Drawers for configuration fields
- Burst-Compliant Code Generated Systems
    - Request Actions via the ActionRequest Component.
    - Start Action at arbitrary position via the ActionRequestAt Component.
    - Define Variable with the ActionVariable Component.
    - If the delegate used to define the Action has a return type, the return type is stored in the ActionResult Component.
        - Results are handled via the user-defined aggregator type.
    - All components are separated via the delegate type (i.e. ActionExecute\<Action1\>, ActionExecute\<Action2\>, etc...)

## TODO
- Field Transformations
- Undo/Redo functionality
- Update documentation
- Unit & Integration Tests
- Node Groups
- Node Colours