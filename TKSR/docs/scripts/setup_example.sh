#!/bin/bash
# ============================================================
# Setup Example - VRMAgentHost boot-time setup sequence
# Equivalent to API Test Console "Setup example" scenario
# ============================================================

SERVER="http://localhost:34560"
#SERVER="http://192.168.1.123:34560"

step() {
    local label="$1"
    local query="$2"
    local wait_sec="${3:-0}"
    echo -e "\033[36m${label}\033[0m"
    curl -s "$SERVER/?$query"
    echo
    if [ "$wait_sec" != "0" ]; then
        sleep "$wait_sec"
    fi
}

echo "==============================================="
echo "Setup Example - VRMAgentHost"
echo "==============================================="
echo

step "[1/22] Load VRM (sample01)" \
    "target=vrm&cmd=load&file=sample01.vrm" 1

step "[2/22] Enable LipSync (channel 0, scale 1)" \
    "target=lipSync&cmd=audiosync_on&channel=0&scale=1"

step "[3/22] Enable auto blink (freq 1500)" \
    "target=animation&cmd=auto_blink&enable=true&freq=1500"

step "[4/22] Allow drag objects" \
    "target=server&cmd=allow_drag_objects&enable=true"

step "[5/22] Enable stay-on-top" \
    "target=server&cmd=stay_on_top&enable=true" 1

step "[6/22] Enable FacePoke + SpringBone" \
    "target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&useSpringBone=true" 1

step "[7/22] Play Idle_calm_02 pose" \
    "target=animation&cmd=play&id=Idle_calm_02&seamless=y"

step "[8/22] Hide wing menus" \
    "target=wingsys&cmd=menus_hide" 1

step "[9/22] Wing menu labels face camera" \
    "target=wingsys&cmd=labels&enable=true&face=camera&fg=FFFFFF&bg=00000080" 1

step "[10/22] Auto-hide wing menus after 60s" \
    "target=wingsys&cmd=menus_hide&auto_hide=true&time=60000" 1

step "[11/22] Attach wing menus to avatar" \
    "target=wingsys&cmd=follow_avatar&enable=true" 1

step "[12/22] Show wing menus" \
    "target=wingsys&cmd=menus_show" 1

step "[13/22] Auto-hide resize/move UI after 15s" \
    "target=server&cmd=resize_move_ui&auto_hide=true&time=15000"

step "[14/22] Overwrite chest depth to -0.03" \
    "target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&channel=chest&op=set&facePokeDepth=-0.03"

step "[15/22] Preset / channel=all (sphere)" \
    "target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&channel=all&op=set&facePokeRadius=0.03&facePokeForceGain=0.3&pokeMethod=sphere&pokeFalloffMultiplier=1.0&pokeHeight=0.05&pokeBaseStrength=0.005&pokeMaxDynamicStrength=0.015&pokeShapeScale=1.0&facePokeDepth=0.02&visibleFacePoke=false"

step "[16/22] Preset / channel=head (auto falloff)" \
    "target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&channel=head&op=set&facePokeRadius=0.02&facePokeForceGain=0.25&pokeMethod=sphere&pokeFalloffMultiplier=0&pokeHeight=0.04&pokeBaseStrength=0.004&pokeMaxDynamicStrength=0.012&pokeShapeScale=0.8&facePokeDepth=0.02&visibleFacePoke=false"

step "[17/22] Preset / channel=chest" \
    "target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&channel=chest&op=set&facePokeRadius=0.04&facePokeForceGain=0.4&pokeMethod=cone&pokeFalloffMultiplier=1.0&pokeHeight=0.06&pokeBaseStrength=0.008&pokeMaxDynamicStrength=0.02&pokeShapeScale=1.2&facePokeDepth=0.01&visibleFacePoke=false"

step "[18/22] Enable FacePoke + SpringBone (re-apply)" \
    "target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&useSpringBone=true" 1

step "[19/22] Load body partition settings" \
    "target=vrm&cmd=body_partitioning&op=load&filename=body_partitions.json" 2

step "[20/22] Load anima definitions" \
    "target=anima_system&cmd=load_defs&filename=anima_system_defs.json"

step "[21/22] Clear anima logs" \
    "target=anima_system&cmd=clear_execute_logs"

step "[22/22] Start anima polling" \
    "target=anima_system&cmd=polling&enable=true"

echo
echo "==============================================="
echo -e "\033[32mSetup Complete!\033[0m"
echo "==============================================="
