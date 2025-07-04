if "%VRMAH_ENDPOINT%"=="" set VRMAH_ENDPOINT=localhost:34560


curl "http://%VRMAH_ENDPOINT%/?target=vrm&cmd=load&file=sample01.vrm"
timeout /t 3

curl "http://%VRMAH_ENDPOINT%/?target=lipSync&cmd=audiosync_on&channel=2&scale=10"
timeout /t 2


curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=autoBlink&enabled=true&freq=1500"
timeout /t 1

