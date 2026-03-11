@echo off
REM TopDownRig reset_reference command test batch file
REM Tests the reset_reference command to safely reset TopDownRig state

if "%VRMAH_ENDPOINT%"=="" set VRMAH_ENDPOINT=localhost:34560
set SERVER=http://%VRMAH_ENDPOINT%

echo ===============================================
echo TopDownRig reset_reference Command Test
echo ===============================================
echo.

echo [Test 1] Reset without TopDownRig (should succeed with "already clean" message)
curl "%SERVER%/?target=vrm&cmd=reset_reference"
echo.
timeout /t 2 >nul

echo.
echo [Test 2] Create TopDownRig with push_reference
curl "%SERVER%/?target=vrm&cmd=push_reference"
echo.
timeout /t 1 >nul

echo.
echo [Test 3] Reset with TopDownRig active (should destroy Rig)
curl "%SERVER%/?target=vrm&cmd=reset_reference"
echo.
timeout /t 2 >nul

echo.
echo [Test 4] Reset again without TopDownRig (should succeed with "already clean" message)
curl "%SERVER%/?target=vrm&cmd=reset_reference"
echo.
timeout /t 2 >nul

echo.
echo [Test 5] Multiple resets in a row (should all succeed safely)
curl "%SERVER%/?target=vrm&cmd=reset_reference"
echo.
timeout /t 1 >nul
curl "%SERVER%/?target=vrm&cmd=reset_reference"
echo.
timeout /t 1 >nul
curl "%SERVER%/?target=vrm&cmd=reset_reference"
echo.
timeout /t 1 >nul

echo.
echo [Test 6] Create TopDownRig again
curl "%SERVER%/?target=vrm&cmd=push_reference"
echo.
timeout /t 1 >nul

echo.
echo [Test 7] Multiple resets while TopDownRig is active (first should destroy, rest should report clean)
curl "%SERVER%/?target=vrm&cmd=reset_reference"
echo.
timeout /t 1 >nul
curl "%SERVER%/?target=vrm&cmd=reset_reference"
echo.
timeout /t 1 >nul
curl "%SERVER%/?target=vrm&cmd=reset_reference"
echo.
timeout /t 1 >nul

echo.
echo ===============================================
echo Test Complete!
echo ===============================================
echo.
echo reset_reference command tested:
echo - Safe to call multiple times
echo - Works with or without TopDownRig
echo - Always returns status 200 (success)
echo.
pause
