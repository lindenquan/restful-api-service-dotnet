<#
.SYNOPSIS
    Build and development automation script for the Prescription Order API.

.DESCRIPTION
    PowerShell 7.5+ script to provide commands for building,
    testing, running, and Docker operations.

.PARAMETER Command
    The command to execute.

.PARAMETER Env
    Environment for test-api-e2e and test-load commands (local, dev, stage). Default: local

.PARAMETER LoadTestArgs
    Arguments to pass to load tests (e.g., 'load', 'concurrency', or empty for all)

.EXAMPLE
    ./build.ps1 run
    ./build.ps1 test-api-e2e -Env dev
    ./build.ps1 test-load
    ./build.ps1 test-load -LoadTestArgs concurrency
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet(
        'run', 'local', 'dev', 'stage', 'demo',
        'build', 'test', 'test-coverage', 'test-api-e2e', 'test-load',
        'format', 'clean', 'restore',
        'docker-up', 'docker-up-api', 'docker-down',
        'docker-build-lambda', 'docker-build-eks', 'docker-build-all',
        'ultimate', 'help'
    )]
    [string]$Command = 'help',

    [Parameter()]
    [ValidateSet('local', 'dev', 'stage')]
    [string]$Env = 'local',

    [Parameter()]
    [string]$LoadTestArgs = ''
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
        $global:LASTEXITCODE = 0  # Reset before running command
        & $ScriptBlock
        # Check exit code for native commands (dotnet, docker, etc.)
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit code $LASTEXITCODE"
        }
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
            dotnet run --project src/Infrastructure
        }
    }
    'local' {
        Invoke-BuildCommand -Name "Run API (local)" -ScriptBlock {
            dotnet run --project src/Infrastructure --environment local
        }
    }
    'dev' {
        Invoke-BuildCommand -Name "Run API (dev)" -ScriptBlock {
            dotnet run --project src/Infrastructure --environment dev
        }
    }
    'stage' {
        Invoke-BuildCommand -Name "Run API (stage)" -ScriptBlock {
            dotnet run --project src/Infrastructure --environment stage
        }
    }
    'demo' {
        Write-Host "[>] Starting Demo App..." -ForegroundColor Cyan
        Write-Host "    Building DemoApp..." -ForegroundColor Gray

        # Build first to catch any errors
        dotnet build src/DemoApp --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[FAIL] Build failed" -ForegroundColor Red
            exit 1
        }

        $port = 5081
        $url = "http://localhost:$port"

        Write-Host "    Starting web server at $url" -ForegroundColor Gray
        Write-Host "    Press Ctrl+C to stop" -ForegroundColor Gray
        Write-Host ""

        # Open browser after a short delay (in background)
        Start-Job -ScriptBlock {
            param($url)
            Start-Sleep -Seconds 2
            Start-Process $url
        } -ArgumentList $url | Out-Null

        # Run the app (this blocks until Ctrl+C)
        dotnet run --project src/DemoApp --urls $url --no-build
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
            # For local E2E tests, start the full API stack in Docker
            # This mimics testing against a real deployed API
            & $PSCommandPath docker-up-api
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

    'test-load' {
        $totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        if ($Env -eq 'local') {
            # For local load tests, start the full API stack in Docker
            & $PSCommandPath docker-up-api
        }
        try {
            $loadTestArgsDisplay = if ($LoadTestArgs) { " ($LoadTestArgs)" } else { " (all)" }
            Invoke-BuildCommand -Name "Run load tests$loadTestArgsDisplay" -ScriptBlock {
                if ($LoadTestArgs) {
                    dotnet run --project tests/Tests.LoadTests -- $LoadTestArgs
                } else {
                    dotnet run --project tests/Tests.LoadTests
                }
            }.GetNewClosure()
        }
        finally {
            if ($Env -eq 'local') {
                Invoke-BuildCommand -Name "Stop Docker services" -ScriptBlock {
                    docker compose down
                }
            }
        }
        $totalStopwatch.Stop()
        Write-Host "`n[TIME] Total load test time: $(Format-Duration $totalStopwatch.Elapsed)" -ForegroundColor Magenta
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
            docker compose up -d mongodb redis --wait
        }
        Invoke-BuildCommand -Name "Run database migrations" -ScriptBlock {
            docker compose run --rm --no-deps db-migrate
        }
    }
    'docker-up-api' {
        Invoke-BuildCommand -Name "Start MongoDB + Redis + API" -ScriptBlock {
            docker compose up -d --build --wait api
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

        Write-Step 2 "Format code"
        & $PSCommandPath format

        Write-Step 3 "Build solution"
        & $PSCommandPath build

        Write-Step 4 "Run unit tests"
        Invoke-BuildCommand -Name "Run unit tests" -ScriptBlock {
            dotnet test tests/Tests --verbosity minimal
        }

        Write-Step 5 "Start Docker (MongoDB + Redis + API)"
        & $PSCommandPath docker-up-api

        try {
            Write-Step 6 "Run API E2E tests"
            $env:ASPNETCORE_ENVIRONMENT = 'local'
            Invoke-BuildCommand -Name "Run API E2E tests" -ScriptBlock {
                dotnet test tests/Tests.Api.E2E --verbosity minimal
            }

            Write-Step 7 "Run load tests (CI mode)"
            Invoke-BuildCommand -Name "Run load tests" -ScriptBlock {
                dotnet run --project tests/Tests.LoadTests -- --ci
            }
        }
        finally {
            Write-Step 8 "Stop Docker services"
            & $PSCommandPath docker-down
        }

        $totalStopwatch.Stop()
        Write-Header "[OK] Complete CI/CD Pipeline Successful!"
        Write-Host "[TIME] Total pipeline time: $(Format-Duration $totalStopwatch.Elapsed)" -ForegroundColor Magenta
        Write-Host "`nSummary:" -ForegroundColor Green
        Write-Host "  - Clean: [OK]" -ForegroundColor Green
        Write-Host "  - Format: [OK]" -ForegroundColor Green
        Write-Host "  - Build: [OK]" -ForegroundColor Green
        Write-Host "  - Unit Tests: [OK]" -ForegroundColor Green
        Write-Host "  - Docker: [OK]" -ForegroundColor Green
        Write-Host "  - E2E Tests: [OK]" -ForegroundColor Green
        Write-Host "  - Load Tests: [OK]" -ForegroundColor Green
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
  demo                Run Demo App (Blazor WebAssembly UI) and open browser
  build               Build the solution
  test                Run unit tests
  test-coverage       Run tests with coverage
  test-api-e2e        Run API E2E tests against real API service
                      -Env local: Tests against API in Docker (localhost:8080)
                      -Env dev|stage: Tests against deployed API (requires API_BASE_URL)
  test-load           Run load/concurrency tests with NBomber
                      -Env local: Tests against API in Docker (localhost:8080)
                      -LoadTestArgs: 'load' | 'concurrency' | '' (all)
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

Complete Pipeline:
  ultimate            Run complete CI/CD pipeline:
                      clean → format → build → unit tests →
                      docker-up-api → E2E tests → load tests (CI mode) → docker-down

Examples:
  ./build.ps1 run
  ./build.ps1 demo
  ./build.ps1 test
  ./build.ps1 test-api-e2e -Env dev
  ./build.ps1 test-load
  ./build.ps1 test-load -LoadTestArgs concurrency
  ./build.ps1 ultimate
'@
        Write-Host $helpText
    }
}
