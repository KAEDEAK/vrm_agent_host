@echo off
chcp 65001
if "%VRMAH_ENDPOINT%"=="" set VRMAH_ENDPOINT=localhost:34560

echo 左右の歩行動作を開始します...
echo.

REM シームレスアニメーションを有効化
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=autoPrepareSeamless&enable=true"

REM 自動瞬きを有効化
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=autoBlink&enabled=true&freq=2000"

:loop
REM 初期位置に戻す
echo 初期位置にリセットします...
curl "http://%VRMAH_ENDPOINT%/?target=multiple&cmd=exec_all&target=vrm&cmd=setLoc&xyz=0,0,0&target=vrm&cmd=setRot&xyz=0,0,0"
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=generic&seamless=y"
timeout /t 2 >nul

REM 右を向く（Y軸 90度）
echo 右を向きます...
curl "http://%VRMAH_ENDPOINT%/?target=vrm&cmd=rotate&to=0,90,0&duration=750&delta=20,80"
timeout /t 1 >nul

REM 右に3歩歩く（歩行アニメーション付き）
echo 右に3歩歩きます...
curl "http://%VRMAH_ENDPOINT%/?target=multiple&cmd=exec_all&target=animation&cmd=play&id=Other_walk&target=vrm&cmd=move&to=3,0,0&duration=3000&delta=100"
timeout /t 3 >nul

REM 立ち止まって振り返る準備
echo 立ち止まります...
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_generic&seamless=y"
timeout /t 1 >nul

REM 左を向く（反転 - Y軸 -90度）
echo 振り返ります...
curl "http://%VRMAH_ENDPOINT%/?target=vrm&cmd=rotate&to=0,-90,0&duration=1000&delta=20,60,20"
timeout /t 1 >nul

REM 左に6歩歩く（右に戻って更に左へ）
echo 左に6歩歩きます...
curl "http://%VRMAH_ENDPOINT%/?target=multiple&cmd=exec_all&target=animation&cmd=play&id=Other_walk&target=vrm&cmd=move&to=-3,0,0&duration=6000&delta=100"
timeout /t 6 >nul

REM 立ち止まる
echo 立ち止まります...
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_generic&seamless=y"
timeout /t 1 >nul

REM 右を向く（正面方向に戻る準備）
echo 右を向きます...
curl "http://%VRMAH_ENDPOINT%/?target=vrm&cmd=rotate&to=0,90,0&duration=750&delta=20,80"
timeout /t 1 >nul

REM 中央に戻る
echo 中央に戻ります...
curl "http://%VRMAH_ENDPOINT%/?target=multiple&cmd=exec_all&target=animation&cmd=play&id=Other_walk&target=vrm&cmd=move&to=0,0,0&duration=3000&delta=100"
timeout /t 3 >nul

REM 正面を向く
echo 正面を向きます...
curl "http://%VRMAH_ENDPOINT%/?target=vrm&cmd=rotate&to=0,0,0&duration=750&delta=20,80"
timeout /t 1 >nul

REM 手を振る
echo 手を振ります...
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Other_wave_hand&seamless=n"
timeout /t 2 >nul

REM 通常のアイドル状態に戻る
echo アイドル状態に戻ります...
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_generic&seamless=y"
timeout /t 2 >nul

