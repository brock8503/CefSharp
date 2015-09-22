// Copyright © 2010-2015 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using CefSharp.Internals;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

namespace CefSharp.BrowserSubprocess
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Kernel32.OutputDebugString("BrowserSubprocess starting up with command line: " + String.Join("\n", args));

            int result;

            using (var subprocess = Create(args))
            {
                result = subprocess.Run();
            }

            Kernel32.OutputDebugString("BrowserSubprocess shutting down.");
            return result;
        }

        private static CefSubProcess Create(IEnumerable<string> args)
        {
            const string typePrefix = "--type=";
            var typeArgument = args.SingleOrDefault(arg => arg.StartsWith(typePrefix));
            var wcfEnabled = args.Any(a => a.StartsWith(CefSharpArguments.WcfEnabledArgument));

            var type = typeArgument.Substring(typePrefix.Length);

            switch (type)
            {
                case "renderer":
                {
                    return wcfEnabled ? new CefRenderProcess(args) : new CefSubProcess(args);
                }
                case "gpu-process":
                { 
                    return new CefSubProcess(args);
                }
                case "ppapi":
                {
                    // HACK ALERT: PPAPI and the flashplayer.dll are executing a 
                    // "cmd.exe /c echo NOT SANDBOXED" on init of the PPAPI flash player. 
                    // This shows a cmd.exe window above all other windows for a second.
                    // This is a poor user experience and until we can get that bug fixed 
                    // we are trying our best to kill cmd.exe spawned from this parent process
                    Task t = Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            int myId = Process.GetCurrentProcess().Id;
                            while (true)
                            {
                                var cmd = Process.GetProcesses().
                                                     Where(pr => pr.ProcessName == "cmd");

                                if (cmd != null)
                                {
                                    foreach (var process in cmd)
                                    {
                                        if(myId == ParentProcessUtil.GetParentProcess(process.Id).Id)
                                        {
                                            process.Kill();
                                            Kernel32.OutputDebugString("Kill the nasty echo");
                                            return; 
                                        }
                                    }
                                }

                                Thread.Sleep(100);
                            }
                        }
                        catch
                        {
                            Kernel32.OutputDebugString("Failed to kill that cmd");
                        }
                    });
                    return new CefSubProcess(args);
                }
                default:
                {
                    return new CefSubProcess(args);
                }
            }
        }
    }
}
