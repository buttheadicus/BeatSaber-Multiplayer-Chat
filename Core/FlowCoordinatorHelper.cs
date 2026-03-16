using System.Reflection;
using HMUI;

namespace MultiplayerChat.Core;

/// <summary>
/// Helper to get the topmost (youngest child) FlowCoordinator for presenting from the correct parent.
/// Presenting from the top flow when in the lobby avoids "MainMenuViewController is inactive" errors.
/// </summary>
public static class FlowCoordinatorHelper
{
    private static FieldInfo? GetChildField()
    {
        var t = typeof(FlowCoordinator);
        return t.GetField("_childFlowCoordinator", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? t.GetField("childFlowCoordinator", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? t.GetField("m_ChildFlowCoordinator", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    private static readonly FieldInfo? ChildField = GetChildField();

    /// <summary>
    /// Gets the topmost flow coordinator in the hierarchy (the one with no active child).
    /// Use this as the parent when presenting from the lobby.
    /// </summary>
    public static FlowCoordinator GetTopFlowCoordinator(FlowCoordinator flow)
    {
        if (flow == null) return flow!;
        if (ChildField == null) return flow;

        var child = ChildField.GetValue(flow) as FlowCoordinator;
        return child != null ? GetTopFlowCoordinator(child) : flow;
    }

    /// <summary>
    /// Gets the child flow coordinator of the given flow, or null if none.
    /// </summary>
    public static FlowCoordinator? GetChildFlowCoordinator(FlowCoordinator flow)
    {
        if (flow == null || ChildField == null) return null;
        return ChildField.GetValue(flow) as FlowCoordinator;
    }
}
