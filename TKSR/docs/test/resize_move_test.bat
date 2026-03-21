@echo off
if "%VRMAH_ENDPOINT%"=="" set VRMAH_ENDPOINT=localhost:34560
echo Testing resize_move command
echo.

echo === Set window to 1024x768 at position (100,100) ===
curl -s "http://%VRMAH_ENDPOINT%/?target=server&cmd=resize_move&left=100&top=100&width=1024&height=768"
echo.
echo.

pause

echo === Set window to 800x600 at position (200,200) ===
curl -s "http://%VRMAH_ENDPOINT%/?target=server&cmd=resize_move&left=200&top=200&width=800&height=600"
echo.
echo.

pause

echo === Test minimum size constraints (should fail) ===
curl -s "http://%VRMAH_ENDPOINT%/?target=server&cmd=resize_move&left=0&top=0&width=300&height=200"
echo.
echo.

pause

echo === Test missing parameters (should fail) ===
curl -s "http://%VRMAH_ENDPOINT%/?target=server&cmd=resize_move&left=0&top=0&width=800"
echo.
echo.

pause

echo === Test invalid parameters (should fail) ===
curl -s "http://%VRMAH_ENDPOINT%/?target=server&cmd=resize_move&left=abc&top=def&width=800&height=600"
echo.
echo.

pause
