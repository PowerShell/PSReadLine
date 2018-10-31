@{
RootModule = 'PSReadLine3.psm1'
NestedModules = @("Microsoft.PowerShell.PSReadLine3.dll")
ModuleVersion = '3.0.0'
GUID = '5714753b-2afd-4492-a5fd-01d9e2cff8b5'
Author = 'Microsoft Corporation'
CompanyName = 'Microsoft Corporation'
Copyright = '(c) Microsoft Corporation. All rights reserved.'
Description = 'Great command line editing in the PowerShell console host'
PowerShellVersion = '5.0'
DotNetFrameworkVersion = '4.6.1'
CLRVersion = '4.0.0'
FormatsToProcess = 'PSReadLine3.format.ps1xml'
AliasesToExport = @()
FunctionsToExport = 'PSConsoleHostReadLine'
CmdletsToExport = 'Get-PSReadLine3KeyHandler','Set-PSReadLine3KeyHandler','Remove-PSReadLine3KeyHandler',
                  'Get-PSReadLine3Option','Set-PSReadLine3Option'
HelpInfoURI = 'https://aka.ms/powershell71-help'
}
