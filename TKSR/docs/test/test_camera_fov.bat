@echo off
if "%VRMAH_ENDPOINT%"=="" set VRMAH_ENDPOINT=localhost:34560
echo ========================================
echo Camera FOV Control Test
echo ========================================

set SERVER=http://%VRMAH_ENDPOINT%

echo.
echo [Test 1] Set FOV to 30 degrees (narrow view)
curl -s "%SERVER%/?target=camera&cmd=fov&value=30"
echo.

timeout /t 2 /nobreak > nul

echo.
echo [Test 2] Set FOV to 60 degrees (default)
curl -s "%SERVER%/?target=camera&cmd=fov&value=60"
echo.

timeout /t 2 /nobreak > nul

echo.
echo [Test 3] Set FOV to 90 degrees (wide view)
curl -s "%SERVER%/?target=camera&cmd=fov&value=90"
echo.

timeout /t 2 /nobreak > nul

echo.
echo [Test 4] Set FOV to 120 degrees (very wide view)
curl -s "%SERVER%/?target=camera&cmd=fov&value=120"
echo.

timeout /t 2 /nobreak > nul

echo.
echo [Test 5] Error test - no value parameter
curl -s "%SERVER%/?target=camera&cmd=fov"
echo.

echo.
echo [Test 6] Error test - invalid value (text)
curl -s "%SERVER%/?target=camera&cmd=fov&value=abc"
echo.

echo.
echo [Test 7] Error test - out of range (0)
curl -s "%SERVER%/?target=camera&cmd=fov&value=0"
echo.

echo.
echo [Test 8] Error test - out of range (180)
curl -s "%SERVER%/?target=camera&cmd=fov&value=180"
echo.

echo.
echo [Test 9] Boundary test - minimum valid value (1)
curl -s "%SERVER%/?target=camera&cmd=fov&value=1"
echo.

timeout /t 2 /nobreak > nul

echo.
echo [Test 10] Boundary test - maximum valid value (179)
curl -s "%SERVER%/?target=camera&cmd=fov&value=179"
echo.

timeout /t 2 /nobreak > nul

echo.
echo [Test 11] Reset to default FOV (60)
curl -s "%SERVER%/?target=camera&cmd=fov&value=60"
echo.

echo.
echo ========================================
echo Test completed
echo ========================================
pause
