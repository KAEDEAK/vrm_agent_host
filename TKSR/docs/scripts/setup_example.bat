@echo off
REM ============================================================
REM Setup Example - VRMAgentHost boot-time setup sequence
REM Equivalent to API Test Console "Setup example" scenario
REM ============================================================

set SERVER=http://localhost:34560

echo ===============================================
echo Setup Example - VRMAgentHost
echo ===============================================
echo.

echo [1/22] Load VRM (sample01)
curl "%SERVER%/?target=vrm&cmd=load&file=sample01.vrm"
echo.
timeout /t 1 

echo [2/22] Enable LipSync (channel 0, scale 1)
curl "%SERVER%/?target=lipSync&cmd=audiosync_on&channel=0&scale=1"
echo.

echo [3/22] Enable auto blink (freq 1500)
curl "%SERVER%/?target=animation&cmd=auto_blink&enable=true&freq=1500"
echo.

echo [4/22] Allow drag objects
curl "%SERVER%/?target=server&cmd=allow_drag_objects&enable=true"
echo.

echo [5/22] Enable stay-on-top
curl "%SERVER%/?target=server&cmd=stay_on_top&enable=true"
echo.
timeout /t 1 

echo [6/22] Enable FacePoke + SpringBone
curl "%SERVER%/?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&useSpringBone=true"
echo.
timeout /t 1 

echo [7/22] Play Idle_calm_02 pose
curl "%SERVER%/?target=animation&cmd=play&id=Idle_calm_02&seamless=y"
echo.

echo [8/22] Hide wing menus
curl "%SERVER%/?target=wingsys&cmd=menus_hide"
echo.
timeout /t 1 

echo [9/22] Wing menu labels face camera
curl "%SERVER%/?target=wingsys&cmd=labels&enable=true&face=camera&fg=FFFFFF&bg=00000080"
echo.
timeout /t 1 

echo [10/22] Auto-hide wing menus after 60s
curl "%SERVER%/?target=wingsys&cmd=menus_hide&auto_hide=true&time=60000"
echo.
timeout /t 1 

echo [11/22] Attach wing menus to avatar
curl "%SERVER%/?target=wingsys&cmd=follow_avatar&enable=true"
echo.
timeout /t 1 

echo [12/22] Show wing menus
curl "%SERVER%/?target=wingsys&cmd=menus_show"
echo.
timeout /t 1 

echo [13/22] Auto-hide resize/move UI after 15s
curl "%SERVER%/?target=server&cmd=resize_move_ui&auto_hide=true&time=15000"
echo.

echo [14/22] Overwrite chest depth to -0.03
curl "%SERVER%/?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&channel=chest&op=set&facePokeDepth=-0.03"
echo.

echo [15/22] Preset / channel=all (sphere)
curl "%SERVER%/?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&channel=all&op=set&facePokeRadius=0.03&facePokeForceGain=0.3&pokeMethod=sphere&pokeFalloffMultiplier=1.0&pokeHeight=0.05&pokeBaseStrength=0.005&pokeMaxDynamicStrength=0.015&pokeShapeScale=1.0&facePokeDepth=0.02&visibleFacePoke=false"
echo.

echo [16/22] Preset / channel=head (auto falloff)
curl "%SERVER%/?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&channel=head&op=set&facePokeRadius=0.02&facePokeForceGain=0.25&pokeMethod=sphere&pokeFalloffMultiplier=0&pokeHeight=0.04&pokeBaseStrength=0.004&pokeMaxDynamicStrength=0.012&pokeShapeScale=0.8&facePokeDepth=0.02&visibleFacePoke=false"
echo.

echo [17/22] Preset / channel=chest
curl "%SERVER%/?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&channel=chest&op=set&facePokeRadius=0.04&facePokeForceGain=0.4&pokeMethod=cone&pokeFalloffMultiplier=1.0&pokeHeight=0.06&pokeBaseStrength=0.008&pokeMaxDynamicStrength=0.02&pokeShapeScale=1.2&facePokeDepth=0.01&visibleFacePoke=false"
echo.

echo [18/22] Enable FacePoke + SpringBone (re-apply)
curl "%SERVER%/?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&useSpringBone=true"
echo.
timeout /t 1 

echo [19/22] Load body partition settings
curl "%SERVER%/?target=vrm&cmd=body_partitioning&op=load&filename=body_partitions.json"
echo.
timeout /t 2 

echo [20/22] Load anima definitions
curl "%SERVER%/?target=anima_system&cmd=load_defs&filename=anima_system_defs.json"
echo.

echo [21/22] Clear anima logs
curl "%SERVER%/?target=anima_system&cmd=clear_execute_logs"
echo.

echo [22/22] Start anima polling
curl "%SERVER%/?target=anima_system&cmd=polling&enable=true"
echo.

echo.
echo ===============================================
echo Setup Complete!
echo ===============================================
pause
