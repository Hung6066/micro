@echo off
REM MCP Harness — stdio bridge to Agent Harness container
REM Called by OpenCode as: cmd /c "scripts\mcp-harness.cmd"
REM Waits for container readiness, then connects stdio.

REM Maximum wait time (30 seconds = 10 tries * ~3s each)
set MAX_RETRIES=10
set RETRY_COUNT=0

:retry
docker inspect his-hope-agentharness --format "{{.State.Health.Status}}" 2>nul | findstr "healthy" >nul
if %errorlevel% equ 0 goto :connect

set /a RETRY_COUNT=RETRY_COUNT+1
if %RETRY_COUNT% geq %MAX_RETRIES% goto :connect

REM Wait ~2 seconds before retrying
ping -n 3 127.0.0.1 >nul
goto :retry

:connect
docker exec -i his-hope-agentharness dotnet /app/His.Hope.AgentHarness.Mcp.dll --stdio
