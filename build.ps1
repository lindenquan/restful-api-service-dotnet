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
        'build', 'test', 'test-coverage', 'test-api-e2e',
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
        $apiProcess = $null
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

        Write-Step 5 "Start MongoDB + Redis"
        & $PSCommandPath docker-up

        Write-Step 6 "Start API (background)"
        $apiProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src/Infrastructure" -PassThru -WindowStyle Hidden
        Write-Host "  Waiting for API to be ready..." -ForegroundColor Gray
        $maxRetries = 30
        $ready = $false
        for ($i = 0; $i -lt $maxRetries; $i++) {
            Start-Sleep -Seconds 1
            try {
                $response = Invoke-WebRequest -Uri "http://localhost:8080/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
                if ($response.StatusCode -eq 200) {
                    Write-Host "  API is ready (PID: $($apiProcess.Id))" -ForegroundColor Green
                    $ready = $true
                    break
                }
            } catch { }
        }
        if (-not $ready) {
            throw "API failed to start within timeout"
        }

        try {
            Write-Step 7 "Run API E2E tests"
            $env:ASPNETCORE_ENVIRONMENT = 'local'
            Invoke-BuildCommand -Name "Run API E2E tests" -ScriptBlock {
                dotnet test tests/Tests.Api.E2E --verbosity minimal
            }
        }
        finally {
            Write-Step 8 "Stop API"
            if ($apiProcess -and -not $apiProcess.HasExited) {
                Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
                Write-Host "  API stopped" -ForegroundColor Green
            }

            Write-Step 9 "Stop Docker services"
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
        Write-Host "  - API: [OK]" -ForegroundColor Green
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
  test-api-e2e        Run API E2E tests against real API service
                      -Env local: Tests against API in Docker (localhost:8080)
                      -Env dev|stage: Tests against deployed API (requires API_BASE_URL)
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
