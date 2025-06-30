# Specification

This document summarizes implementation notes and conventions for the Visual Avator Sock project.

## Coding Guidelines
- All public APIs should validate parameters and return structured JSON responses with status codes.
- Configuration values are loaded from `config.json` at startup. Use the `ServerConfig` singleton to access settings.
- Avoid hard coding file paths; use `Assets/Resources` or configuration values.

## Wave Playback
- The `/waveplay/` endpoint accepts mono 16bit 48kHz WAV data via HTTP POST.
- Maximum payload size is `wavePayloadMaxBytes` (default 5,000,000 bytes).
- Concurrency behavior is controlled by `wavePlaybackConcurrency`: `interrupt`, `reject`, or `queue`.

## [Implemented] VRMA Hips Offset Caching
On first playback of a VRMA animation, the difference between the VRMA end pose and
the target pose is stored as a position and rotation offset for the Hips bone.
When the same VRM model plays the animation again, the cached offset is applied to
keep the pose aligned. Offsets are kept only in memory and are not configurable or
persistent across models.

## Lip Sync Channels
- Channel `0`: WavePlayback
- Channel `1`: ExternalAudio
- Channel `2`: Microphone

## Error Handling
- Invalid requests return 4xx status codes with JSON `{ "error": "code", "detail": "..." }`.
- Unexpected exceptions are logged and return 500 status.

## Testing
[Implemented] - Batch files under `TEST/batch/` provide basic integration tests.
[Implemented] - Use `TEST/vrm_agent_host_test.json` as a sample configuration.
[Implemented] - Set the `VRMAH_ENDPOINT` environment variable to override the HTTP host and port used by the batch scripts (default `localhost:34560`).
[Implemented] - Wave playback test commands (`waveplay_ping.bat`, `waveplay_play_sample.bat`) are available under `TEST/batch/cmd`.
[Implemented] - Mouth and shape scripts for phoneme testing (`animation_mouth_*.bat`, `animation_shape_*.bat`) now include A/I/U/E/O variants.

