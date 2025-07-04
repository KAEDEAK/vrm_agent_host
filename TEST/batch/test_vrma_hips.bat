@echo off
REM Automated test for VRM Host: verify hips position/rotation after VRMA playback
REM Requires sample01.vrm and VRMA_01.vrma under 00_vrm/ and 00_vrma/ directories.

if "%VRMAH_ENDPOINT%"=="" set VRMAH_ENDPOINT=localhost:34560
chcp 65001 >nul

set "VRM_FILE=sample01.vrm"
set "VRMA_FILE=VRMA_01.vrma"

REM --- Load VRM ---
curl "http://%VRMAH_ENDPOINT%/?target=vrm&cmd=load&file=%VRM_FILE%"
REM give server a moment to update
timeout /t 2 >nul

REM Capture hips position/rotation before playing VRMA
for /f "delims=" %%a in ('curl -s "http://%VRMAH_ENDPOINT%/?target=vrm&cmd=getLoc"') do set "POS_BEFORE=%%a"
for /f "delims=" %%a in ('curl -s "http://%VRMAH_ENDPOINT%/?target=vrm&cmd=getRot"') do set "ROT_BEFORE=%%a"

REM --- Play VRMA once ---
curl "http://%VRMAH_ENDPOINT%/?target=animation&cmd=play&file=%VRMA_FILE%&continue=false"
REM wait until animation playback should be finished
timeout /t 5 >nul

REM Capture hips position/rotation after playing VRMA
for /f "delims=" %%a in ('curl -s "http://%VRMAH_ENDPOINT%/?target=vrm&cmd=getLoc"') do set "POS_AFTER=%%a"
for /f "delims=" %%a in ('curl -s "http://%VRMAH_ENDPOINT%/?target=vrm&cmd=getRot"') do set "ROT_AFTER=%%a"

ECHO.
ECHO Before : %POS_BEFORE% / %ROT_BEFORE%
ECHO After  : %POS_AFTER% / %ROT_AFTER%
ECHO.

set RESULT=0
if "%POS_BEFORE%"=="%POS_AFTER%" (
  echo Hips position OK
) else (
  echo Hips position NG
  set RESULT=1
)

if "%ROT_BEFORE%"=="%ROT_AFTER%" (
  echo Hips rotation OK
) else (
  echo Hips rotation NG
  set RESULT=1
)

exit /b %RESULT%
