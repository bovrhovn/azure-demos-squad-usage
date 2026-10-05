[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$NoBuild,

    [Parameter(ValueFromRemainingArguments)]
    [string[]]$ApplicationArguments
)

Import-Module (Join-Path $PSScriptRoot 'DemoLaunchers.psm1') -Force
Invoke-CopilotDynamicModuleDemo @PSBoundParameters
