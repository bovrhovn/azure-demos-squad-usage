[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$ProjectDir,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$NoBuild,

    [Parameter(ValueFromRemainingArguments)]
    [string[]]$ApplicationArguments
)

Import-Module (Join-Path $PSScriptRoot 'DemoLaunchers.psm1') -Force
Invoke-SquadHelloDemo @PSBoundParameters
