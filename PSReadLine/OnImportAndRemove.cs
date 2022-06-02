using System;
using System.IO;
using System.Management.Automation;
using System.Reflection;

namespace Microsoft.PowerShell.PSReadLine
{
    public class OnModuleImportAndRemove : IModuleAssemblyInitializer, IModuleAssemblyCleanup
    {
        public void OnRemove(PSModuleInfo module)
        {
            AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssembly;
        }

        public void OnImport()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        /// <summary>
        ///     Load the correct 'Polyfiller' assembly based on the runtime.
        /// </summary>
        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            if (args.Name !=
                "Microsoft.PowerShell.PSReadLine.Polyfiller, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")
                return null;

            var root = Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location);
            var subd = Environment.Version.Major >= 6 ? "net6plus" : "net462";
            var path = Path.Combine(root, subd, "Microsoft.PowerShell.PSReadLine.Polyfiller.dll");

            return Assembly.LoadFrom(path);
        }
    }
}