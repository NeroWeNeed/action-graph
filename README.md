# Action-Graph
Action-Graph is a Unity Editor Extension tool designed to create state-machine styled assets for pluggable behaviour in Unity DOTS. 

## Motivation
Action-Graph was designed as an aid for programmers instead of a replacement. Graph based logic is inherently unintuitive to write, but annoying to deal with in a visual scripting environment. This extension was designed as a middle ground, allowing all the nodes necessary for your use case to be easy to write in code, while being easy to organize in the graph editor.

## Features
- Define Actions with delegates and the ActionDefinition Asset.
- Node-based editor for combining segments of code.
    - Automatically finds relevant nodes through code
    - Variable support
    - Customizable Field Drawers for configuration fields
- Burst-Compliant Code Generated Systems
    - Request Actions via the ActionRequest Component.
    - Start Action at arbitrary position via the ActionRequestAt Component.
    - Define Variable with the ActionVariable Component.
    - If the delegate used to define the Action has a return type, the return type is stored in the ActionResult Component.
        - Results are handled via the user-defined aggregator type.
    - All components are separated via the delegate type (i.e. ActionExecute\<Action1\>, ActionExecute\<Action2\>, etc...)
    


