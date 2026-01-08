using System;
using System.IO;
using System.Management.Automation;
using System.Reflection;

namespace Microsoft.PowerShell.PSReadLine
{
    public class OnModuleImportAndRemove : IModuleAssemblyInitializer, IModuleAssemblyCleanup
    {
        public void OnImport()
        {
            // Module initialization - reserved for future use
        }

        public void OnRemove(PSModuleInfo module)
        {
            // Module cleanup - reserved for future use
        }
    }
}
