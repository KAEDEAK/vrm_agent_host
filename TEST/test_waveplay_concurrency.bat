@echo off
echo Testing waveplay concurrency modes...
echo.

echo Testing interrupt mode (default):
curl -X POST -H "Content-Type: audio/wav" -H "X-Audio-ID: test-interrupt-1" --data-binary "@sample.wav" http://localhost:34560/waveplay/
timeout /t 1 >nul
curl -X POST -H "Content-Type: audio/wav" -H "X-Audio-ID: test-interrupt-2" --data-binary "@sample.wav" http://localhost:34560/waveplay/
echo.

echo Testing queue mode:
echo Note: You need to change wavePlaybackConcurrency to "queue" in config.json first
echo.

echo Testing reject mode:
echo Note: You need to change wavePlaybackConcurrency to "reject" in config.json first
echo.

echo Testing ping:
curl -X GET http://localhost:34560/waveplay/ping
echo.

pause
