@echo off
setlocal enabledelayedexpansion
REM Usage: wingsys_manual_highlight_test.bat [host:port] [wing_index] [duration]
set HOST=%1
if "%HOST%"=="" set HOST=localhost:34560
set IDX=%2
if "%IDX%"=="" set IDX=5
set DUR=%3
if "%DUR%"=="" set DUR=2.0

echo === Highlight wing %IDX% for %DUR%s ===
curl -sS "http://%HOST%/?target=wingsys&cmd=highlight&action=set&wing_index=%IDX%&duration=%DUR%"
echo.
echo === Status (menus_status) ===
curl -sS "http://%HOST%/?target=wingsys&cmd=menus_status"
echo.
echo === Clear highlight on wing %IDX% ===
curl -sS "http://%HOST%/?target=wingsys&cmd=highlight&action=clear&wing_index=%IDX%"
echo.
endlocal

