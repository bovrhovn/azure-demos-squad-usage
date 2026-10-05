BeforeAll {
    $scriptsDirectory = Split-Path -Parent $PSScriptRoot
    Import-Module (Join-Path $scriptsDirectory 'DemoLaunchers.psm1') -Force
}

Describe 'Demo launchers' {
    BeforeEach {
        $global:demoLauncherDotnetArguments = @()
        $global:demoLauncherProjectDirectoryDuringLaunch = $null
        $global:LASTEXITCODE = 0

        Mock dotnet -ModuleName DemoLaunchers {
            $global:demoLauncherDotnetArguments = $args
            $global:demoLauncherProjectDirectoryDuringLaunch = $env:PROJECTDIR
            $global:LASTEXITCODE = 0
        }
    }

    It 'runs the GitHub Copilot demo with build and application parameters' {
        Invoke-GitHubCopilotDemo -Configuration Release -NoBuild -ApplicationArguments @('--sample-option', 'sample-value')

        $global:demoLauncherDotnetArguments | Should -Contain 'run'
        $global:demoLauncherDotnetArguments | Should -Contain '--no-build'
        $global:demoLauncherDotnetArguments | Should -Contain '--sample-option'
        $global:demoLauncherDotnetArguments | Should -Contain 'sample-value'
        $global:demoLauncherDotnetArguments | Should -Contain (Join-Path (Split-Path -Parent $scriptsDirectory) 'src\SquadDemos\SquadDemos.GHCopilot\SquadDemos.GHCopilot.csproj')
    }

    It 'runs the Squad demo with a scoped PROJECTDIR value' {
        $originalProjectDir = $env:PROJECTDIR
        try {
            Invoke-SquadHelloDemo -ProjectDir $TestDrive -ApplicationArguments @('--sample-option')

            $global:demoLauncherProjectDirectoryDuringLaunch | Should -Be (Resolve-Path -LiteralPath $TestDrive).Path
            $env:PROJECTDIR | Should -Be $originalProjectDir
            $global:demoLauncherDotnetArguments | Should -Contain (Join-Path (Split-Path -Parent $scriptsDirectory) 'src\SquadDemos\SquadDemos.SquadHello\SquadDemos.SquadHello.csproj')
        }
        finally {
            $env:PROJECTDIR = $originalProjectDir
        }
    }

    It 'runs the web demo with forwarded application parameters' {
        Invoke-CopilotDynamicModuleDemo -ApplicationArguments @('--urls', 'http://localhost:5050')

        $global:demoLauncherDotnetArguments | Should -Contain '--urls'
        $global:demoLauncherDotnetArguments | Should -Contain 'http://localhost:5050'
        $global:demoLauncherDotnetArguments | Should -Contain (Join-Path (Split-Path -Parent $scriptsDirectory) 'src\SquadDemos\SquadDemos.Web.CopilotDynamicModule\SquadDemos.Web.CopilotDynamicModule.csproj')
    }
}
