// Decompiled with JetBrains decompiler
// Type: NobleTitles.Log
// Assembly: NobleTitles, Version=1.2.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1ECF68F4-B6F2-4499-99A9-27E0EE6B0499
// Assembly location: G:\OneDrive - Mathis Consulting, LLC\Desktop\NobleTitles.dll

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;


#nullable enable
namespace NobleTitles
{
  internal class Log : LogBase
  {
    private const string BeginMultiLine = "=======================================================================================================================\\";
    private const string EndMultiLine = "=======================================================================================================================/";
    public readonly string Module;
    public readonly string LogDir;
    public readonly string LogFile;
    public readonly string LogPath;

    protected TextWriter Writer { get; set; }

    protected bool LastWasMultiline { get; set; } = false;

    public override void Print(string line)
    {
      if (this.Writer == null)
        return;
      this.LastWasMultiline = false;
      this.Writer.WriteLine(line);
      this.Writer.Flush();
    }

    public override void Print(List<string> lines)
    {
      if (this.Writer == null || lines.Count == 0)
        return;
      if (lines.Count == 1)
      {
        this.Print(lines[0]);
      }
      else
      {
        if (!this.LastWasMultiline)
          this.Writer.WriteLine("=======================================================================================================================\\");
        this.LastWasMultiline = true;
        foreach (string line in lines)
          this.Writer.WriteLine(line);
        this.Writer.WriteLine("=======================================================================================================================/");
        this.Writer.Flush();
      }
    }

    public Log(bool truncate = false, string? logName = null)
    {
      string path1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mount and Blade II Bannerlord");
      this.Module = this.GetType().FullName;
      this.LogDir = Path.Combine(path1, "Logs");
      this.LogFile = logName == null ? this.GetType().Namespace + ".log" : this.GetType().Namespace + "." + logName + ".log";
      this.LogPath = Path.Combine(this.LogDir, this.LogFile);
      Directory.CreateDirectory(this.LogDir);
      bool flag = File.Exists(this.LogPath);
      try
      {
        this.Writer = TextWriter.Synchronized((TextWriter) new StreamWriter(this.LogPath, !truncate, Encoding.UTF8, 65536));
      }
      catch (Exception ex)
      {
        Console.WriteLine("================================  EXCEPTION  ================================");
        Console.WriteLine(this.Module + ": Failed to create StreamWriter!");
        Console.WriteLine("Path: " + this.LogPath);
        Console.WriteLine(string.Format("Truncate: {0}", (object) truncate));
        Console.WriteLine(string.Format("Preexisting Path: {0}", (object) flag));
        Console.WriteLine("Exception Information:");
        Console.WriteLine(string.Format("{0}", (object) ex));
        Console.WriteLine("=============================================================================");
        throw;
      }
      this.Writer.NewLine = "\n";
      List<string> lines = new List<string>()
      {
        string.Format("{0} created at: {1:yyyy/MM/dd H:mm zzz}", (object) this.Module, (object) DateTimeOffset.Now)
      };
      if (flag && !truncate)
      {
        this.Writer.WriteLine();
        this.Writer.WriteLine();
        lines.Add("NOTE: Any prior log messages in this file may have no relation to this session.");
      }
      this.Print(lines);
    }

    ~Log()
    {
      try
      {
        this.Writer.Dispose();
      }
      catch (Exception ex)
      {
      }
      finally
      {
        // ISSUE: explicit finalizer call
        //base.Finalize();
      }
    }
  }
}
