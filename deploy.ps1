param(
    [string]$Action = "build"
)

$projectRoot = "C:\Users\Administrator\Desktop\work\CU-MCP-Bridge"
$modDll = "$projectRoot\BepInEx\bin\Release\net472\CU-MCP-Mod.dll"
$gamePlugins = "C:\Program Files (x86)\Steam\steamapps\common\Casualties Unknown Demo\BepInEx\plugins"

switch ($Action) {
    "build" {
        Write-Host "Building C# mod..."
        dotnet build "$projectRoot\BepInEx\CU-MCP-Mod.csproj" -c Release
        if ($LASTEXITCODE -ne 0) { exit 1 }
        Write-Host "Build OK" -ForegroundColor Green
    }

    "deploy" {
        Write-Host "Killing game process..."
        Stop-Process -Name "CasualtiesUnknown" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500

        powershell -Command "& { dotnet build '$projectRoot\BepInEx\CU-MCP-Mod.csproj' -c Release }"
        if ($LASTEXITCODE -ne 0) { exit 1 }

        Write-Host "Copying DLL..."
        Remove-Item "$gamePlugins\CU-MCP-Mod.dll" -Force -ErrorAction SilentlyContinue
        Copy-Item $modDll "$gamePlugins\CU-MCP-Mod.dll" -Force
        Copy-Item "$projectRoot\BepInEx\bin\Release\net472\Newtonsoft.Json.dll" "$gamePlugins\Newtonsoft.Json.dll" -Force -ErrorAction SilentlyContinue

        Write-Host "Deploy OK - start the game and run Python server" -ForegroundColor Green
    }

    "test" {
        Write-Host "Running C# tests..."
        dotnet test "$projectRoot\tests\csharp\CU-MCP-Mod.Tests.csproj" --no-restore --filter "FullyQualifiedName~ProtocolTests"

        Write-Host "Running Python tests..."
        cd "$projectRoot\tests\python"
        python -m pytest -v
        cd $projectRoot
    }

    "python-server" {
        Write-Host "Starting Python MCP server (stdio)..."
        cd "$projectRoot"
        python src/bridge_server/server.py
    }

    default {
        Write-Host "Usage: .\deploy.ps1 [build|deploy|test|python-server]"
    }
}
