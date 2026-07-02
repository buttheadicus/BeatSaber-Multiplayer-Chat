using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace MultiplayerChat.Core;

internal static class MpChatBootstrapExit
{
    internal static void ScheduleHardExitSoon(string threadName)
    {
        try
        {
            Application.Quit();
        }
        catch
        {
            /* ignore */
        }

        var t = new Thread(() => ForceExitAfterDelay(threadName))
        {
            Name = threadName,
            IsBackground = true
        };
        t.Start();
    }

    private static void ForceExitAfterDelay(string threadName)
    {
        Thread.Sleep(450);

        try
        {
            Application.Quit();
        }
        catch
        {
            /* ignore */
        }

        try
        {
            Process.GetCurrentProcess().Kill();
        }
        catch
        {
            try
            {
                Environment.Exit(0);
            }
            catch
            {
                /* ignore */
            }
        }
    }
}
