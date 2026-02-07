# PowerShell script to run the Spiderly Shared tests
# Requires .NET SDK 9.0 or later

Write-Host "Installing .NET SDK if not present..." -ForegroundColor Yellow
# Install .NET SDK 9.0 (if not already installed)
# Download from: https://dotnet.microsoft.com/download

Write-Host "Restoring packages..." -ForegroundColor Green
dotnet restore

Write-Host "Running tests..." -ForegroundColor Green
dotnet test Spiderly.Shared.Tests/ --verbosity normal --configuration Release

Write-Host "Tests completed!" -ForegroundColor Green
