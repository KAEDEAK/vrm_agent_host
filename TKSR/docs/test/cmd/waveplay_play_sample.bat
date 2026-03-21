@echo off
if "%VRMAH_ENDPOINT%"=="" set VRMAH_ENDPOINT=localhost:34560
if "%1"=="" (
  echo Usage: %0 path_to_wav
  exit /b 1
)
curl -H "Content-Type: audio/wav" --data-binary "@%1" "http://%VRMAH_ENDPOINT%/waveplay/"
