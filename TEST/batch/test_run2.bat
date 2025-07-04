if "%VRMAH_ENDPOINT%"=="" set VRMAH_ENDPOINT=localhost:34560
@echo off
chcp 65001

:start
REM 初期位置に移動
curl "http://%VRMAH_ENDPOINT%/?target=multiple&cmd=exec_all&target=vrm&cmd=setLoc&xyz=0,0,0&target=vrm&cmd=setRot&xyz=0,0,0"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=autoBlink&enabled=true&freq=1500"

REM 奥に移動
curl "http://%VRMAH_ENDPOINT%/?target=multiple&cmd=exec_all&target=vrm&cmd=setLoc&xyz=0,0,-10&target=vrm&cmd=setRot&xyz=0,180,0"
timeout /t 1

REM 手を振る
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Other_WaveArm_01&seamless=n"
timeout /t 3

REM 初期の姿勢に戻す
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=generic&seamless=y"
timeout /t 1

REM 手前に走ってくる
curl "http://%VRMAH_ENDPOINT%/?target=multiple&cmd=exec_all&target=animation&cmd=play&id=Other_run&target=vrm&cmd=move&to=0,0,0.4&duration=3000&delta=100"
timeout /t 3

REM 初期の姿勢に戻す
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=generic&seamless=y"
timeout /t 2

REM にっこりする
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=autoBlink&enabled=false"
curl "http://%VRMAH_ENDPOINT%/?target=multiple&cmd=exec_all&target=animation&cmd=play&id=Idle_cute&seamless=y&target=animation&cmd=shape&word=Happy&seamless=y"
timeout /t 1
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=autoBlink&enabled=true&freq=1500"

REM 初期の姿勢に戻す
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=shape&word=reset&seamless=y"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=generic&seamless=y"
timeout /t 2

REM ランダムな方向に移動
set /a rand_x=%random% %% 5 - 2
set /a rand_z=%random% %% 5 - 10
REM 移動方向に回転
curl "http://%VRMAH_ENDPOINT%/?target=multiple&cmd=exec_all&target=vrm&cmd=rotate&to=0,%rand_x%,0&duration=750&delta=20,80"
timeout /t 1
REM 移動する
curl "http://%VRMAH_ENDPOINT%/?target=multiple&cmd=exec_all&target=animation
