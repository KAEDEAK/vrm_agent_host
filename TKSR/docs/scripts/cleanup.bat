@echo off
setlocal

chcp 65001

REM Terminating VRMAgentHost processes

taskkill /F /IM "VRMAgentHost-X1.exe" /T


echo 🎉 COMPLETED


endlocal

timeout /t 5