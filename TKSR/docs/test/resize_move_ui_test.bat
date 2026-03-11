@echo off
if "%VRMAH_ENDPOINT%"=="" set VRMAH_ENDPOINT=localhost:34560
echo Testing resize_move_ui command
echo.

echo === Enable resize_move_ui ===
curl -s "http://%VRMAH_ENDPOINT%/?target=server&cmd=resize_move_ui&enable=true"
echo.
echo.

pause

echo === Disable resize_move_ui ===
curl -s "http://%VRMAH_ENDPOINT%/?target=server&cmd=resize_move_ui&enable=false"
echo.
echo.

pause

echo === Invalid parameter test (missing enable) ===
curl -s "http://%VRMAH_ENDPOINT%/?target=server&cmd=resize_move_ui"
echo.
echo.

pause

echo === Invalid parameter test (invalid enable value) ===
curl -s "http://%VRMAH_ENDPOINT%/?target=server&cmd=resize_move_ui&enable=invalid"
echo.
echo.

pause
