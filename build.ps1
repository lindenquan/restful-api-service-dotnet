<#
.SYNOPSIS
    Build and development automation script for the Prescription Order API.

.DESCRIPTION
    PowerShell 7.5+ script to provide commands for building,
    testing, running, and Docker operations.

.PARAMETER Command
    The command to execute.

.PARAMETER Env
    Environment for test-api-e2e command (local, dev, stage). Default: local

.EXAMPLE
    ./build.ps1 run
    ./build.ps1 test-api-e2e -Env dev
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet(
        'run', 'local', 'dev', 'stage',
        'build', 'test', 'test-coverage', 'test-api-e2e', 'test-all',
        'format', 'clean', 'restore',
        'docker-up', 'docker-up-api', 'docker-down',
        'docker-build-lambda', 'docker-build-eks', 'docker-build-all',
        'ultimate', 'help'
    )]
    [string]$Command = 'help',

    [Parameter()]
    [ValidateSet('local', 'dev', 'stage')]
    [string]$Env = 'local'
)

$ErrorActionPreference = 'Stop'

function Format-Duration([TimeSpan]$duration) {
    $parts = @()
    if ($duration.Hours -gt 0) { $parts += "$($duration.Hours)h" }
    if ($duration.Minutes -gt 0) { $parts += "$($duration.Minutes)m" }
    if ($duration.Seconds -gt 0 -or $parts.Count -eq 0) { $parts += "$($duration.Seconds)s" }
    $parts += "$($duration.Milliseconds)ms"
    return $parts -join ' '
}

function Invoke-BuildCommand {
    param(
        [string]$Name,
        [scriptblock]$ScriptBlock
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    Write-Host "[>] $Name..." -ForegroundColor Cyan

    try {
        & $ScriptBlock
        $stopwatch.Stop()
        $duration = Format-Duration $stopwatch.Elapsed
        Write-Host "[OK] $Name [$duration]" -ForegroundColor Green
    }
    catch {
        $stopwatch.Stop()
        $duration = Format-Duration $stopwatch.Elapsed
        Write-Host "[FAIL] $Name [$duration]" -ForegroundColor Red
        throw
    }
}

function Write-Header($text) {
    Write-Host "`n=========================================" -ForegroundColor Cyan
    Write-Host $text -ForegroundColor Cyan
    Write-Host "=========================================`n" -ForegroundColor Cyan
}

function Write-Step($step, $text) {
    Write-Host "`n[Step $step] $text" -ForegroundColor Yellow
}

switch ($Command) {
    'run' {
        Invoke-BuildCommand -Name "Run API" -ScriptBlock {
            dotnet run --project src/Adapters
        }
    }
    'local' {
        Invoke-BuildCommand -Name "Run API (local)" -ScriptBlock {
            dotnet run --project src/Adapters --environment local
        }
    }
    'dev' {
        Invoke-BuildCommand -Name "Run API (dev)" -ScriptBlock {
            dotnet run --project src/Adapters --environment dev
        }
    }
    'stage' {
        Invoke-BuildCommand -Name "Run API (stage)" -ScriptBlock {
            dotnet run --project src/Adapters --environment stage
        }
    }
    'build' {
        Invoke-BuildCommand -Name "Build solution" -ScriptBlock {
            dotnet build
        }
    }
    'test' {
        Invoke-BuildCommand -Name "Run unit tests" -ScriptBlock {
            dotnet test tests/Tests --verbosity minimal
        }
    }
    'test-coverage' {
        Invoke-BuildCommand -Name "Run tests with coverage" -ScriptBlock {
            dotnet test tests/Tests --collect:"XPlat Code Coverage"
        }
    }
    'test-api-e2e' {
        $totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        if ($Env -eq 'local') {
            Invoke-BuildCommand -Name "Start Docker (MongoDB + Redis)" -ScriptBlock {
                docker compose up -d mongodb redis
                Start-Sleep -Seconds 10
            }
        }
        try {
            $env:ASPNETCORE_ENVIRONMENT = $Env
            Invoke-BuildCommand -Name "Run API E2E tests ($Env)" -ScriptBlock {
                dotnet test tests/Tests.Api.E2E --verbosity minimal
            }
        }
        finally {
            if ($Env -eq 'local') {
                Invoke-BuildCommand -Name "Stop Docker services" -ScriptBlock {
                    docker compose down
                }
            }
        }
        $totalStopwatch.Stop()
        Write-Host "`n[TIME] Total E2E test time: $(Format-Duration $totalStopwatch.Elapsed)" -ForegroundColor Magenta
    }
    'test-all' {
        $totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        Write-Header "Running ALL Tests"
        Write-Step 1 "API Unit Tests"
        & $PSCommandPath test
        Write-Step 2 "API E2E Tests"
        & $PSCommandPath test-api-e2e -Env local
        $totalStopwatch.Stop()
        Write-Header "[OK] All Tests Passed!"
        Write-Host "[TIME] Total time: $(Format-Duration $totalStopwatch.Elapsed)" -ForegroundColor Magenta
    }
    'format' {
        Invoke-BuildCommand -Name "Format code" -ScriptBlock {
            dotnet format
        }
    }
    'clean' {
        Invoke-BuildCommand -Name "Clean build artifacts" -ScriptBlock {
            Get-ChildItem -Path src, tests -Include bin, obj -Recurse -Directory -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
        }
    }
    'restore' {
        Invoke-BuildCommand -Name "Restore packages" -ScriptBlock {
            dotnet restore
        }
    }
    'docker-up' {
        Invoke-BuildCommand -Name "Start MongoDB + Redis" -ScriptBlock {
            docker compose up -d mongodb redis
            Start-Sleep -Seconds 10
        }
    }
    'docker-up-api' {
        Invoke-BuildCommand -Name "Start MongoDB + Redis + API" -ScriptBlock {
            docker compose up -d --build
            Start-Sleep -Seconds 20
        }
    }
    'docker-down' {
        Invoke-BuildCommand -Name "Stop Docker services" -ScriptBlock {
            docker compose down
        }
    }
    'docker-build-lambda' {
        Invoke-BuildCommand -Name "Build Lambda Docker image" -ScriptBlock {
            docker build -f Dockerfile.lambda -t prescription-api-lambda:latest .
        }
    }
    'docker-build-eks' {
        Invoke-BuildCommand -Name "Build EKS Docker image" -ScriptBlock {
            docker build -f Dockerfile.eks -t prescription-api-eks:latest .
        }
    }
    'docker-build-all' {
        $totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        & $PSCommandPath docker-build-lambda
        & $PSCommandPath docker-build-eks
        $totalStopwatch.Stop()
        Write-Host "`n[TIME] Total build time: $(Format-Duration $totalStopwatch.Elapsed)" -ForegroundColor Magenta
    }
    'ultimate' {
        $totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        Write-Header "Starting Complete CI/CD Pipeline"
        Write-Step 1 "Clean build artifacts"
        & $PSCommandPath clean
        Write-Step 2 "Build solution"
        & $PSCommandPath build
        Write-Step 3 "Run all tests"
        & $PSCommandPath test-all
        $totalStopwatch.Stop()
        Write-Header "[OK] Complete CI/CD Pipeline Successful!"
        Write-Host "[TIME] Total pipeline time: $(Format-Duration $totalStopwatch.Elapsed)" -ForegroundColor Magenta
        Write-Host "`nSummary:" -ForegroundColor Green
        Write-Host "  - Clean: [OK]" -ForegroundColor Green
        Write-Host "  - Build: [OK]" -ForegroundColor Green
        Write-Host "  - Unit Tests: [OK]" -ForegroundColor Green
        Write-Host "  - E2E Tests: [OK]" -ForegroundColor Green
    }
    'help' {
        $helpText = @'
Prescription Order API - Build Commands

Usage: ./build.ps1 <command> [-Env <environment>]

API Commands:
  run                 Run API (uses environment from .env file)
  local               Run API in local mode (explicit override)
  dev                 Run API in dev mode (explicit override)
  stage               Run API in stage mode (explicit override)
  build               Build the solution
  test                Run unit tests
  test-coverage       Run tests with coverage
  test-api-e2e        Run API E2E tests (default: local with Docker)
                      Use -Env dev|stage to test against other environments
  format              Format code
  clean               Clean build artifacts
  restore             Restore packages

Docker Commands:
  docker-up           Start MongoDB + Redis only
  docker-up-api       Start MongoDB + Redis + API (all in Docker)
  docker-down         Stop all services
  docker-build-lambda Build AWS Lambda image
  docker-build-eks    Build AWS EKS image
  docker-build-all    Build all Docker images

Test Commands:
  test-all            Run all tests (unit + E2E)
  ultimate            Run complete CI/CD pipeline

Examples:
  ./build.ps1 run
  ./build.ps1 test
  ./build.ps1 test-api-e2e -Env dev
  ./build.ps1 ultimate
'@
        Write-Host $helpText
    }
}
