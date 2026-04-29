using System;
using System.Reflection;

namespace MultiplayerChat.AvatarExtras.Utils;

public static class ReflectionUtil
{
    public static U InvokeGenericMethod<U, T, G>(this T obj, string methodName, params object[] args)
    {
        var method = typeof(T).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException("Method " + methodName + " does not exist", nameof(methodName));
        var generic = method.MakeGenericMethod(typeof(G));
        return (U)generic.Invoke(obj, args)!;
    }
}
