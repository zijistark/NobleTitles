// Decompiled with JetBrains decompiler
// Type: NobleTitles.Util
// Assembly: NobleTitles, Version=1.2.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1ECF68F4-B6F2-4499-99A9-27E0EE6B0499
// Assembly location: G:\OneDrive - Mathis Consulting, LLC\Desktop\NobleTitles.dll

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;


#nullable enable
namespace NobleTitles
{
  internal class Util
  {
    internal static LogBase Log = new LogBase();

    internal static bool EnableLog
    {
      get => Util.Log is NobleTitles.Log;
      set
      {
        if (Util.Log is NobleTitles.Log && !value)
        {
          Util.Log = new LogBase();
        }
        else
        {
          if (!(!(Util.Log is NobleTitles.Log) & value))
            return;
          Util.Log = (LogBase) new NobleTitles.Log(true, "debug");
        }
      }
    }

    internal static bool EnableTracer { get; set; } = false;

    internal static class EventTracer
    {
      private static readonly ConcurrentDictionary<string, bool> _stackTraceMap = new ConcurrentDictionary<string, bool>();

      [MethodImpl(MethodImplOptions.NoInlining)]
      internal static void Trace(string extraInfo, int framesToSkip = 1) => Util.EventTracer.Trace(new List<string>()
      {
        extraInfo
      }, framesToSkip + 1);

      [MethodImpl(MethodImplOptions.NoInlining)]
      internal static void Trace(List<string>? extraInfo = null, int framesToSkip = 1)
      {
        if (!Util.EnableTracer || !Util.EnableLog)
          return;
        StackTrace stackTrace = new StackTrace(framesToSkip, true);
        MethodBase method = stackTrace.GetFrames()[0].GetMethod();
        List<string> lines = new List<string>()
        {
          string.Format("Code Event Invoked: {0}.{1}", (object) method.DeclaringType, (object) method.Name),
          string.Format("Real Timestamp:     {0:MM/dd H:mm:ss.fff}", (object) DateTime.Now)
        };
        if (Campaign.Current != null)
        {
          List<string> stringList = lines;
          List<string> collection = new List<string>();
          collection.Add(string.Format("Campaign Time:      {0}", (object) CampaignTime.Now));
          CampaignTime campaignStartTime = Campaign.Current.CampaignStartTime;
          collection.Add(string.Format("  Elapsed Years:    {0:F3}", (object) campaignStartTime.ElapsedYearsUntilNow));
          campaignStartTime = Campaign.Current.CampaignStartTime;
          collection.Add(string.Format("  Elapsed Days:     {0:F2}", (object) campaignStartTime.ElapsedDaysUntilNow));
          campaignStartTime = Campaign.Current.CampaignStartTime;
          collection.Add(string.Format("  Elapsed Hours:    {0:F2}", (object) campaignStartTime.ElapsedHoursUntilNow));
          stringList.AddRange((IEnumerable<string>) collection);
        }
        string str1 = stackTrace.ToString();
        if (str1.Length > 2)
        {
          string str2 = str1.Replace("\r\n", "\n");
          string key = str2.Remove(str2.Length - 1, 1);
          if (Util.EventTracer._stackTraceMap.TryAdd(key, true))
            lines.AddRange((IEnumerable<string>) new List<string>()
            {
              string.Empty,
              "Stack Trace:",
              key
            });
        }
        if (extraInfo != null && extraInfo.Count > 0)
        {
          lines.AddRange((IEnumerable<string>) new List<string>()
          {
            string.Empty,
            "Extra Information:"
          });
          if (extraInfo.Count > 1)
            lines.Add(string.Empty);
          lines.AddRange((IEnumerable<string>) extraInfo);
        }
        Util.Log.Print(lines);
      }
    }
  }
}
