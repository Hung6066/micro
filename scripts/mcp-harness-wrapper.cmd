@echo off
REM MCP Harness Wrapper — waits for container health before connecting
REM Called by OpenCode as: cmd /c "scripts\mcp-harness-wrapper.cmd"

REM Wait for container to be healthy (max 30s)
for /l %%i in (1,1,30) do (
    docker inspect his-hope-agentharness --format "{{.State.Health.Status}}" | findstr "healthy" >nul 2>&1
    if !errorlevel! equ 0 goto :connect
    ping -n 2 127.0.0.1 >nul
)

:connect
docker exec -i his-hope-agentharness dotnet /app/His.Hope.AgentHarness.Mcp.dll --stdio
