# WavePlay Concurrency Mode Guide

## Overview

The waveplay endpoint now supports three different concurrency modes that control how multiple audio requests are handled:

- **interrupt**: Stop current playback and start new audio (default)
- **queue**: Queue new audio requests and play them sequentially
- **reject**: Reject new requests while audio is already playing

## Implementation Details

### Unified Handler Architecture

The system now uses `WavePlaybackHandler` for all concurrency modes, providing a unified architecture with full spatial audio support:

- **All modes**: Uses `WavePlaybackHandler` with complete concurrency control and spatial audio support
- **Legacy fallback**: Falls back to `HandleWavePlaybackRequest` only if WavePlaybackHandler is unavailable

### Configuration

Edit your `config.json` file to change the concurrency mode:

```json
{
  "wavePlaybackConcurrency": "interrupt"
}
```

Valid values:
- `"interrupt"` - Default, always interrupts current playback
- `"queue"` - Queues requests when audio is playing
- `"reject"` - Rejects requests when audio is playing

## Behavior by Mode

### Interrupt Mode (Default)
- **Handler**: WavePlaybackHandler
- **Behavior**: New audio immediately stops current playback
- **Features**: 
  - ✅ Spatial audio positioning at avatar head
  - ✅ Volume control via X-Volume header
  - ✅ Spatial override via X-Spatial header
- **Use Case**: Real-time voice interaction, immediate response

### Queue Mode
- **Handler**: WavePlaybackHandler
- **Behavior**: New audio is queued and played after current audio finishes
- **Features**:
  - ✅ Sequential playback
  - ✅ Queue size limit (10 items max)
  - ✅ Automatic queue cleanup
  - ✅ Spatial audio positioning at avatar head
  - ✅ Volume control via X-Volume header
  - ✅ Spatial override via X-Spatial header
- **Use Case**: Speech synthesis, narration, sequential audio playback

### Reject Mode
- **Handler**: WavePlaybackHandler
- **Behavior**: New audio requests are rejected with HTTP 409 (Conflict) while playing
- **Features**:
  - ✅ Prevents audio overlap
  - ✅ Clear busy status indication
  - ✅ Spatial audio positioning at avatar head
  - ✅ Volume control via X-Volume header
  - ✅ Spatial override via X-Spatial header
- **Use Case**: Single-speaker scenarios, preventing audio conflicts

## API Responses

### Interrupt Mode
```json
{"status": "ok", "id": "audio-123"}
{"status": "interrupted", "prev_id": "audio-122", "id": "audio-123"}
```

### Queue Mode
```json
{"status": "queued", "id": "audio-123"}
{"status": "ok", "id": "audio-123"}  // When playback starts
```

### Reject Mode
```json
{"status": "busy"}  // When audio is already playing
{"status": "ok", "id": "audio-123"}  // When not playing
```

## Migration Notes

### From Previous Versions
- Default behavior remains unchanged (interrupt mode)
- Existing queue/reject functionality is preserved
- No breaking changes to API

### Spatial Audio Considerations
- Spatial audio now works in all concurrency modes (interrupt, queue, reject)
- All modes use WavePlaybackHandler with full spatial audio support
- Audio is positioned at avatar head automatically when VRM model is loaded

## Testing

Use the provided test script:
```bash
TEST/test_waveplay_concurrency.bat
```

Or test manually:
```bash
# Test interrupt (default)
curl -X POST -H "Content-Type: audio/wav" --data-binary "@audio.wav" http://localhost:34560/waveplay/

# Test ping
curl -X GET http://localhost:34560/waveplay/ping
```

## Troubleshooting

### Audio Cutting Out
- **Cause**: Using interrupt mode with rapid audio requests
- **Solution**: Switch to queue mode for sequential playback

### No Spatial Audio
- **Cause**: VRM model not loaded or spatial audio disabled in config
- **Solution**: Load a VRM model and ensure waveSpatializationEnabled is true in config

### Queue Full
- **Cause**: More than 10 items queued in queue mode
- **Solution**: Reduce request frequency or increase processing speed

## Configuration Example

Complete config.json example:
```json
{
  "wavePlaybackEnabled": true,
  "wavePlaybackConcurrency": "queue",
  "wavePlaybackVolume": 1.0,
  "waveSpatializationEnabled": true,
  "wavePayloadMaxBytes": 5000000,
  "lipSyncOffsetMs": 0
}
```

## Log Messages

Look for these log messages to verify behavior:

```
[AnimationServer] Using legacy handler for concurrency mode: queue
[Wave] Queued item audio-123, queue size: 3
[Wave] Processing next queued item audio-124, remaining queue: 2
