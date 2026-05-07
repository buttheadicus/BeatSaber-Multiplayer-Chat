using System.Reflection;
using HMUI;

namespace MultiplayerChat.Core;

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

    public static FlowCoordinator GetTopFlowCoordinator(FlowCoordinator flow)
    {
        if (flow == null) return flow!;
        if (ChildField == null) return flow;

        var child = ChildField.GetValue(flow) as FlowCoordinator;
        return child != null ? GetTopFlowCoordinator(child) : flow;
    }

    public static FlowCoordinator? GetChildFlowCoordinator(FlowCoordinator flow)
    {
        if (flow == null || ChildField == null) return null;
        return ChildField.GetValue(flow) as FlowCoordinator;
    }
}
