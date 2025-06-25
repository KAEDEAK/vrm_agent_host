if "%VRMAH_ENDPOINT%"=="" set VRMAH_ENDPOINT=localhost:34560
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&id=Idle_boyish&seamless=y"
