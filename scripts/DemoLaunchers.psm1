Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-DemoProject {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ProjectRelativePath,

        [ValidateSet('Debug', 'Release')]
        [string]$Configuration = 'Debug',

        [switch]$NoBuild,

        [string[]]$ApplicationArguments,

        [string]$ProjectDir
    )

    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    $projectPath = Join-Path $repositoryRoot $ProjectRelativePath

    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        throw "Demo project was not found at '$projectPath'."
    }

    $dotnetArguments = @('run', '--project', $projectPath, '--configuration', $Configuration)
    if ($NoBuild) {
        $dotnetArguments += '--no-build'
    }

    $dotnetArguments += '--'
    $dotnetArguments += $ApplicationArguments

    $previousProjectDir = $env:PROJECTDIR
    try {
        if ($ProjectDir) {
            $env:PROJECTDIR = (Resolve-Path -LiteralPath $ProjectDir -ErrorAction Stop).Path
        }

        & dotnet @dotnetArguments
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
            throw "The demo exited with code $LASTEXITCODE."
        }
    }
    finally {
        if ($ProjectDir) {
            $env:PROJECTDIR = $previousProjectDir
        }
    }
}

function Invoke-GitHubCopilotDemo {
    [CmdletBinding()]
    param(
        [ValidateSet('Debug', 'Release')]
        [string]$Configuration = 'Debug',

        [switch]$NoBuild,

        [string[]]$ApplicationArguments
    )

    Invoke-DemoProject `
        -ProjectRelativePath 'src\SquadDemos\SquadDemos.GHCopilot\SquadDemos.GHCopilot.csproj' `
        -Configuration $Configuration `
        -NoBuild:$NoBuild `
        -ApplicationArguments $ApplicationArguments
}

function Invoke-SquadHelloDemo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
        [string]$ProjectDir,

        [ValidateSet('Debug', 'Release')]
        [string]$Configuration = 'Debug',

        [switch]$NoBuild,

        [string[]]$ApplicationArguments
    )

    Invoke-DemoProject `
        -ProjectRelativePath 'src\SquadDemos\SquadDemos.SquadHello\SquadDemos.SquadHello.csproj' `
        -Configuration $Configuration `
        -NoBuild:$NoBuild `
        -ApplicationArguments $ApplicationArguments `
        -ProjectDir $ProjectDir
}

function Invoke-CopilotDynamicModuleDemo {
    [CmdletBinding()]
    param(
        [ValidateSet('Debug', 'Release')]
        [string]$Configuration = 'Debug',

        [switch]$NoBuild,

        [string[]]$ApplicationArguments
    )

    Invoke-DemoProject `
        -ProjectRelativePath 'src\SquadDemos\SquadDemos.Web.CopilotDynamicModule\SquadDemos.Web.CopilotDynamicModule.csproj' `
        -Configuration $Configuration `
        -NoBuild:$NoBuild `
        -ApplicationArguments $ApplicationArguments
}

Export-ModuleMember -Function Invoke-GitHubCopilotDemo, Invoke-SquadHelloDemo, Invoke-CopilotDynamicModuleDemo
