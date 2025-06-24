

curl "http://localhost:34560/?target=vrm&cmd=load&file=sample01.vrm"
timeout /t 3

curl "http://localhost:34560/?target=lipSync&cmd=audiosync_on&channel=2&scale=10"
timeout /t 2


curl "http://localhost:34560/?target=animation&cmd=autoBlink&enabled=true&freq=1500"
timeout /t 1

