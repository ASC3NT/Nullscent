# Long Note (LN) System Implementation

## Overview

This system implements **osu!mania lazer / ScoreV2-style Long Notes** with deterministic, replay-safe timing.

## Core Principles

### 1. ScoreV2 Compatibility
- **LN heads and tails are separate judgements**
- Each LN = 2 judgeable events
- Head and tail contribute independently to accuracy and score
- Total objects = regular notes + (LN count × 2)

### 2. Deterministic Timing
- Uses **song time** as source of truth, not frame delta
- State transitions are explicit and frame-rate independent
- Supports accurate replay playback and analytics

### 3. Separate Concerns
- `LongNote.cs`: Gameplay state machine (head/tail judged, hold/break state)
- `DrawableLongNote.cs`: Visual representation (rendering, colors, effects)
- `ManiaPlayfield.Column`: Input handling and integration

---

## State Machine

### States (LongNoteState enum)

```
Idle → Holding → Released → Finished
       ↓
     Broken → Finished
```

| State | Description |
|-------|-------------|
| **Idle** | Waiting for head hit |
| **Holding** | Key held after successful head hit |
| **Broken** | Key released early (tail will be Miss) |
| **Released** | Key released at tail (unused currently) |
| **Finished** | Both head and tail judged |

### State Transitions

1. **Head Hit**:
   - `Idle` → `Holding` (if hit)
   - `Idle` → `Finished` (if missed)

2. **Early Release**:
   - `Holding` → `Broken`
   - **Broken state persists even if key pressed again**

3. **Tail Judgement**:
   - `Holding` → `Finished` (normal release)
   - `Broken` → `Finished` (automatic Miss)

---

## Usage

### Creating a Long Note

```csharp
var longNote = new LongNote(
    startTime: 1000,  // Head timing (ms)
    endTime: 1500,    // Tail timing (ms)
    column: 2
);
```

### Integration in Beatmap Conversion

```csharp
// In ManiaBeatmapConverter
if (hitObject is HoldNote hold)
{
    var ln = new LongNote(hold.StartTime, hold.EndTime, hold.Column);
    maniaObjects.Add(ln);
}
```

### Gameplay Loop

```csharp
// In Column.Update()
if (drawable is DrawableLongNote drawableLN)
{
    // Update hold state every frame
    drawableLN.UpdateHold(currentTime, isKeyHeld);

    // Check for auto-misses
    drawableLN.CheckAutoMiss(currentTime, hitWindows);

    // Apply results when fully judged
    if (drawableLN.HitObject is LongNote ln && ln.IsFullyJudged)
    {
        scoreProcessor.ApplyResult(ln.HeadResult);
        scoreProcessor.ApplyResult(ln.TailResult);
        // Remove drawable
    }
}
```

### Input Handling

```csharp
// Key Press → Judge Head
public void OnKeyPress(double currentTime, ManiaScoreProcessor scoreProcessor)
{
    if (closest is DrawableLongNote drawableLN)
    {
        drawableLN.CheckForResult(currentTime, hitWindows);
        // Don't remove yet - continues to tail
    }
}

// Key Release → Judge Tail
public void OnKeyRelease(double currentTime, ManiaScoreProcessor scoreProcessor)
{
    foreach (var drawable in _drawableHitObjects)
    {
        if (drawable is DrawableLongNote drawableLN)
        {
            drawableLN.OnKeyRelease(currentTime, hitWindows);
        }
    }
}
```

---

## Visual Feedback

### Body Colors

| State | Color | Meaning |
|-------|-------|---------|
| `Idle` | Orange | Approaching |
| `Holding` | Green | Successfully holding |
| `Broken` | Red | Released early |

### Receptor Lighting

```csharp
if (drawableLN.ShouldLightReceptor())
{
    // Light up receptor during hold
}
```

### Head/Tail Rendering

- **Head**: Visible until judged, disappears on hit
- **Body**: Visible during hold/broken states
- **Tail**: Visible until judged, uses thinner marker

---

## Scoring Integration

### ScoreV2 Mode

```csharp
var scoreProcessor = new ManiaScoreProcessor(
    totalNotes: regularNotes,
    totalHoldNotes: longNotes,
    hpDrainRate: 5.0,
    healthDrainEnabled: true,
    isScoreV2: true  // LN heads+tails = 2 objects each
);
```

### Accuracy Calculation

With `isScoreV2 = true`:
- Total objects = notes + (LN × 2)
- Each head and tail uses `AccuracyWeightFor(result)`
- Miss on head → entire LN fails immediately

---

## Edge Cases Handled

### 1. Head Miss
```csharp
// If head is missed:
HeadResult = Miss;
TailResult = Miss;  // Automatic
State = Finished;   // Never enters holding
```

### 2. Early Release
```csharp
// Key released before tail window:
State = Broken;
// Later, tail judgement:
TailResult = Miss;  // Automatic
```

### 3. Broken State Persistence
```csharp
// Even if key pressed again after break:
if (State == Broken)
{
    // Still broken, tail will be Miss
}
```

### 4. Auto-Miss Windows
```csharp
// Head not hit in time:
CheckHeadAutoMiss(currentTime, hitWindows);

// Holding past tail window:
CheckTailAutoMiss(currentTime, hitWindows);
```

---

## Performance Considerations

### Deterministic Updates

```csharp
// ✅ CORRECT: Uses song time
UpdateHold(currentTime, isKeyHeld);

// ❌ WRONG: Frame-delta accumulation
_holdDuration += gameTime.ElapsedGameTime.TotalMilliseconds;
```

### Efficient Rendering

```csharp
// Body height calculated once per frame
int bodyHeight = Math.Max(headY, tailY) - Math.Min(headY, tailY);

// Culling check before drawing
if (bottomY < -50 || topY > screenHeight + 50)
    return;
```

---

## Future Enhancements

### 1. Tail Window Multiplier
```csharp
// ScoreV2: tail windows are 1.5x longer
double tailWindow = hitWindows.WindowFor(result) * 1.5;
```

### 2. Integer Timing Pipeline
```csharp
// osu!mania uses truncated timing internally
int headOffsetMs = (int)Math.Truncate(currentTime - StartTime);
```

### 3. Hold Tick System
```csharp
// Some modes use "hold ticks" for sustain judgement
// Not yet implemented
```

### 4. Break Tolerance
```csharp
// Some games allow brief releases without breaking
// Currently: any release = break
```

---

## Testing Scenarios

### Test Case 1: Perfect LN
```csharp
// Press exactly at head time
OnKeyPress(1000.0);
// Release exactly at tail time
OnKeyRelease(1500.0);
// Expected: Head=Perfect, Tail=Perfect
```

### Test Case 2: Early Release
```csharp
OnKeyPress(1000.0);   // Hit head
OnKeyRelease(1200.0); // Release early (tail at 1500)
// Expected: Head=Perfect, Tail=Miss
```

### Test Case 3: Head Miss
```csharp
// Don't press, let auto-miss trigger
CheckHeadAutoMiss(1200.0);
// Expected: Head=Miss, Tail=Miss, State=Finished
```

### Test Case 4: Late Head, Perfect Tail
```csharp
OnKeyPress(1050.0);   // Late head
OnKeyRelease(1500.0); // Perfect tail
// Expected: Head=Great/Good, Tail=Perfect
```

---

## API Reference

### LongNote Class

| Method | Description |
|--------|-------------|
| `TryJudgeHead(time, windows)` | Judges head when key pressed |
| `UpdateHold(time, isKeyHeld)` | Updates hold state (call every frame) |
| `TryJudgeTail(time, windows)` | Judges tail when key released |
| `CheckHeadAutoMiss(time, windows)` | Auto-miss if head not hit in time |
| `CheckTailAutoMiss(time, windows)` | Auto-miss if held too long |

### DrawableLongNote Class

| Method | Description |
|--------|-------------|
| `Update(...)` | Updates visual state |
| `Draw(...)` | Renders head/body/tail |
| `CheckForResult(...)` | Judges head on key press |
| `OnKeyRelease(...)` | Judges tail on key release |
| `UpdateHold(...)` | Updates hold tracking |
| `CheckAutoMiss(...)` | Checks both auto-miss conditions |
| `ShouldLightReceptor()` | Returns true during hold |

---

## Compatibility Notes

### osu!mania Differences

| Feature | osu!mania lazer | This Implementation |
|---------|-----------------|---------------------|
| Head/Tail Separation | ✅ Yes | ✅ Yes |
| ScoreV2 Support | ✅ Yes | ✅ Yes |
| Tail 1.5x Windows | ✅ Yes | ⏳ Planned |
| Hold Ticks | ✅ Yes | ❌ Not implemented |
| Integer Timing | ✅ Yes (truncated) | ⏳ Planned |
| Break Tolerance | ❌ No | ❌ No |

### Replay Safety

This implementation is **replay-safe** because:
- Uses song time, not frame delta
- Explicit state machine with deterministic transitions
- No accumulated floating-point errors
- Input events can be replayed at different frame rates

---

## Summary

✅ **Implemented**:
- Separate head/tail judgements (ScoreV2-style)
- Deterministic state machine
- Hold/break state tracking
- Auto-miss detection
- Visual feedback (colors, lighting)
- Replay-safe timing

⏳ **Planned**:
- Tail window 1.5x multiplier
- Integer/truncated timing pipeline
- Hold tick system
- Advanced analytics/metrics
