
chcp 65001

REM curl "http://localhost:34560/?target=vrm&cmd=load&file=sample01.vrm"

REM 移動 setLoc,move   x,y,z    0,0,0:初期位置;  0,0,-10:奥;  0,0,0.4:手前
REM 回転 setRoc,rotate x,y,z    0,180,0:背中が見える; 0,0,0:顔が見える;


:start
REM 奥に移動
curl "http://localhost:34560/?target=multiple&cmd=exec_all&target=vrm&cmd=setRot&xyz=0,0,0&target=vrm&cmd=setLoc&xyz=0,0,-10"
curl "http://localhost:34560/?target=animation&cmd=autoBlink&enabled=true&freq=1500"
timeout /t 1

REM 手を振る
curl "http://localhost:34560/?target=animation&cmd=play&id=Other_WaveArm_01&seamless=n"
timeout /t 3

REM もとの姿勢に戻す
curl "http://localhost:34560/?target=animation&cmd=play&id=generic&seamless=y"
timeout /t 1

REM 手前方向（アバターの顔が見える状態。カメラ方向）に走ってくる
curl "http://localhost:34560/?target=multiple&cmd=exec_all&target=animation&cmd=play&id=Other_run&target=vrm&cmd=move&to=0,0,0.38&duration=3000&delta=100"
timeout /t 3

REM もとの姿勢に戻す
curl "http://localhost:34560/?target=animation&cmd=play&id=generic&seamless=y"
timeout /t 2

REM にっこりする
curl "http://localhost:34560/?target=animation&cmd=autoBlink&enabled=false"
curl "http://localhost:34560/?target=multiple&cmd=exec_all&target=animation&cmd=play&id=Idle_cute&seamless=y&target=animation&cmd=shape&word=Happy&seamless=y"
timeout /t 1
curl "http://localhost:34560/?target=animation&cmd=autoBlink&enabled=true&freq=1500"

REM もとの姿勢に戻す
curl "http://localhost:34560/?target=animation&cmd=shape&word=reset&seamless=y"
curl "http://localhost:34560/?target=animation&cmd=play&id=generic&seamless=y"
timeout /t 2

REM 回転して少し奥へ下がる（アバターの背中が見える状態）
curl "http://localhost:34560/?target=multiple&cmd=exec_all&target=animation&cmd=play&id=Other_run&target=vrm&cmd=move&to=0,0,0&duration=500&delta=100&target=vrm&cmd=rotate&to=0,180,0&duration=500&delta=10,90"

REM そのまま奥まで行く
curl "http://localhost:34560/?target=multiple&cmd=exec_all&target=animation&cmd=play&id=Other_run&target=vrm&cmd=move&to=0,0,-10&duration=3000&delta=100"
timeout /t 3

REM プレイヤー方向を向く（顔が見える）
curl "http://localhost:34560/?target=multiple&cmd=exec_all&target=animation&cmd=play&id=generic&seamless=y&target=vrm&cmd=rotate&to=0,0,0&duration=750&delta=20,80"
timeout /t 1

REM ------------------------------

rem ▼ 手を振る
curl "http://localhost:34560/?target=animation&cmd=play&id=Other_WaveArm_01&seamless=n"
timeout /t 3

rem ▼ 向きを左斜め前に調整（Y軸 20°）
curl "http://localhost:34560/?target=vrm&cmd=rotate&to=0,20,0&duration=750&delta=20,80"
timeout /t 1

rem ▼ 走って左斜め前へ（さきほどの奥と手前の間）
curl "http://localhost:34560/?target=multiple&cmd=exec_all&target=animation&cmd=play&id=Other_run&target=vrm&cmd=move&to=2,0,-6&duration=3000&delta=100"
timeout /t 3

rem ▼ 停止
curl "http://localhost:34560/?target=multiple&cmd=exec_all&target=animation&cmd=play&id=Idle_cute&seamless=y"
curl "http://localhost:34560/?target=vrm&cmd=setRot&xyz=0,0,0"

REM その位置からプレイヤーを見る
curl "http://localhost:34560/?target=vrm&cmd=rotate&to=0,-20,0&duration=250&delta=100"
timeout /t 2

rem ▼ 表情リセット
curl "http://localhost:34560/?target=animation&cmd=shape&word=reset&seamless=y"
timeout /t 1

rem ▼ 横移動する前に右向き（Y軸 -90°）に回転
curl "http://localhost:34560/?target=vrm&cmd=rotate&to=0,-90,0&duration=500&delta=20,80"

rem ▼ 左から右へ横移動
curl "http://localhost:34560/?target=multiple&cmd=exec_all&target=animation&cmd=play&id=Other_run&target=vrm&cmd=move&to=-2,0,-6&duration=3000&delta=100"
timeout /t 3

rem ▼ 左を向く（奥を向く。Y軸 180°）
curl "http://localhost:34560/?target=vrm&cmd=rotate&to=0,180,0&duration=750&delta=20,80"
timeout /t 1

rem ▼ 右 → 奥へ走って戻る
curl "http://localhost:34560/?target=multiple&cmd=exec_all&target=animation&cmd=play&id=Other_run&target=vrm&cmd=move&to=0,0,-10&duration=3000&delta=100"
timeout /t 3

curl "http://localhost:34560/?cmd=setRot&xyz=0,-180,0"
curl "http://localhost:34560/?target=vrm&cmd=rotate&to=0,0,0&duration=750&delta=20,80"
timeout /t 1
curl "http://localhost:34560/?cmd=setRot&xyz=0,0,0"

REM もとの姿勢に戻す
curl "http://localhost:34560/?target=animation&cmd=play&id=generic&seamless=y"
timeout /t 2

rem ▼ 到着、笑顔でポーズ
curl "http://localhost:34560/?target=animation&cmd=shape&word=reset&seamless=y"
curl "http://localhost:34560/?target=multiple&cmd=exec_all&target=animation&cmd=shape&blink=0.9,0.0&seamless=y&target=animation&cmd=play&id=Idle_Energetic_02&seamless=y"
timeout /t 3

REM もとの姿勢に戻す
curl "http://localhost:34560/?target=animation&cmd=play&id=generic&seamless=y"
curl "http://localhost:34560/?target=animation&cmd=shape&word=reset"

goto start
