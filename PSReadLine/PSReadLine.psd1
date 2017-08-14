@{
RootModule = 'PSReadLine.psm1'
NestedModules = @("Microsoft.PowerShell.PSReadLine.dll")
ModuleVersion = '2.0'
GUID = '5714753b-2afd-4492-a5fd-01d9e2cff8b5'
Author = 'Microsoft Corporation'
CompanyName = 'Microsoft Corporation'
Copyright = '(c) Microsoft Corporation. All rights reserved.'
Description = 'Great command line editing in the PowerShell console host'
PowerShellVersion = '5.0'
DotNetFrameworkVersion = '4.6.1'
CLRVersion = '4.6.1'
AliasesToExport = @()
FunctionsToExport = 'PSConsoleHostReadline'
CmdletsToExport = 'Get-PSReadlineKeyHandler','Set-PSReadlineKeyHandler','Remove-PSReadlineKeyHandler',
                  'Get-PSReadlineOption','Set-PSReadlineOption'
HelpInfoURI = 'http://go.microsoft.com/fwlink/?LinkId=528806'
}
