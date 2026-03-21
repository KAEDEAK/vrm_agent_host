@echo off
setlocal enabledelayedexpansion
set HOST=%1
if "%HOST%"=="" set HOST=localhost:34560

echo === menus_status ===
curl -sS "http://%HOST%/?target=wingsys&cmd=menus_status"
echo.

echo === enable user_mode and define defaults ===
curl -sS "http://%HOST%/?target=wingsys&cmd=user_mode&enable=true&base=USER"
echo.

echo === show menus and enable labels ===
curl -sS "http://%HOST%/?target=wingsys&cmd=menus_show"
curl -sS "http://%HOST%/?target=wingsys&cmd=labels&enable=true&face=camera"
echo.

echo === interaction (basic) ===
curl -sS "http://%HOST%/?target=wingsys&cmd=menus_interaction_status"
echo.

echo Click any wing (except EXIT), then press any key...
pause > nul

echo === interaction after click (basic) ===
curl -sS "http://%HOST%/?target=wingsys&cmd=menus_interaction_status"
echo.

echo === interaction (detailed) ===
curl -sS "http://%HOST%/?target=wingsys&cmd=menus_interaction_status&detailed=true"
echo.

echo See the click count.
endlocal

pause

