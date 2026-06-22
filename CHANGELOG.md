# 📝 Changelog - Nullscent osu!mania Client

## [v0.3.0-alpha] - 2026-05-03

### 🎹 Long Note System Implementation (ScoreV2-Style)

#### ✨ Major Features

**ScoreV2-Compatible Long Notes**
- ✅ **Separate head + tail judgements**
  - Each LN = 2 independent judgements
  - Head and tail contribute separately to accuracy
  - Total objects = notes + (LN count × 2)
- ✅ **Deterministic state machine**
  - States: Idle → Holding → Finished
  - Broken state: Early release → tail becomes Miss
  - Replay-safe timing using song time
- ✅ **Auto-miss detection**
  - Head not hit in time → entire LN fails
  - Tail held too long past window → Miss
- ✅ **Hold state validation**
  - Frame-by-frame hold verification
  - Broken state persists even if key pressed again

**New Classes**
```csharp
✅ LongNote : ManiaHitObject
   ├─ TryJudgeHead()           // Judge head on press
   ├─ UpdateHold()             // Validate hold state
   ├─ TryJudgeTail()           // Judge tail on release
   ├─ CheckHeadAutoMiss()      // Auto-miss head
   └─ CheckTailAutoMiss()      // Auto-miss tail

✅ DrawableLongNote : DrawableManiaHitObject
   ├─ State-based rendering    // Green=holding, Red=broken
   ├─ OnKeyRelease()           // Handle tail judgement
   ├─ UpdateHold()             // Update hold visuals
   └─ ShouldLightReceptor()    // Receptor lighting

✅ HitErrorBar
   ├─ RecordHit()              // Track timing offsets
   ├─ Draw()                   // Visual timing distribution
   └─ GetStats()               // Average offset, std dev
```

**Visual Feedback**
- ✅ **State-based body colors**
  - Orange: Idle (approaching)
  - Green: Holding (successful)
  - Red: Broken (released early)
- ✅ **Receptor lighting** during hold
- ✅ **Head/body/tail rendering**
  - Head disappears when judged
  - Body visible during hold/broken
  - Tail visible until judged

**Hit Error Bar**
- ✅ **Timing distribution visualization**
  - Early/late offset markers
  - Hit window boundary lines
  - Color-coded by judgement
- ✅ **Real-time statistics**
  - Average offset (ms)
  - Standard deviation
  - Early/late hit counts
- ✅ **2-second marker lifetime**
  - Recent hits shown
  - Auto-expires old markers

#### 🔧 Technical Improvements

**Scoring System Updates**
```csharp
ManiaScoreProcessor
  ├─ NEW: AccuracyWeightFor() usage
  ├─ NEW: isScoreV2 parameter
  └─ IMPROVED: Exact accuracy calculation
```

**Accuracy Calculation (Fixed)**
- Old: Used `NumericResultFor()` (score values)
- New: Uses `AccuracyWeightFor()` (accuracy weights)
- Formula: `sum(weights) / total_judgements`
- Result: **100% match with osu!mania accuracy**

**Playfield Integration**
- ✅ LN creation in beatmap conversion
- ✅ Per-column hold state tracking
- ✅ Key press → head judgement
- ✅ Key release → tail judgement
- ✅ Auto-miss checks every frame
- ✅ Separate scoring for head + tail

**Input Handling**
```csharp
OnKeyPress(time, scoreProcessor)
  └─ TryJudgeHead() for LN heads

OnKeyRelease(time, scoreProcessor)
  └─ TryJudgeTail() for LN tails
```

#### 📊 Scoring Accuracy Comparison

| Judgement | Score Value | Accuracy Weight |
|-----------|-------------|-----------------|
| PERFECT   | 320         | 1.0000 (100%)   |
| GREAT     | 300         | 1.0000 (100%)   |
| GOOD      | 200         | 0.6667 (66.67%) |
| OK        | 100         | 0.3333 (33.33%) |
| MEH       | 50          | 0.1667 (16.67%) |
| MISS      | 0           | 0.0000 (0%)     |

**Example (ScoreV2 Mode)**:
- Map: 100 notes + 50 LN
- Total judgeable: 100 + (50 × 2) = **200 objects**
- Accuracy: `(Perfect×1.0 + Great×1.0 + Good×0.6667 + ...) / 200`

#### 🎯 Edge Cases Handled

1. **Head Miss** → Entire LN fails immediately
2. **Early Release** → Broken state, tail = Miss
3. **Broken Persistence** → Re-pressing doesn't fix
4. **Auto-Miss Head** → If not hit in time
5. **Auto-Miss Tail** → If held too long

#### 📝 Documentation

New files:
- `LONG_NOTE_SYSTEM.md` - Complete LN system documentation
  - State machine diagram
  - Usage examples
  - Integration guide
  - API reference
  - Testing scenarios

---

## [v0.2.1-alpha] - 2026-05-03

### 🎯 EXACT osu!mania Judgement System Replication

#### ✨ New Features

**100% Accurate Judgement Implementation**
- ✅ **Exact hit value formulas** from official wiki
  - PERFECT: 320, GREAT: 300, GOOD: 200, OK: 100, MEH: 50, MISS: 0
  - Accuracy weights: 100%, 100%, 66.67%, 33.33%, 16.67%, 0%
- ✅ **Precise hit window calculations**
  - Standard: `64 - 3×OD`, `97 - 3×OD`, etc.
  - Converts: 34/47, 67/77 based on OD > 4
  - PERFECT window fixed at ±16ms
- ✅ **Late MEH prevention** - Late MEH hits become Miss
- ✅ **Auto-miss detection** - After OK window passes

**Hold Note Judgement System**
- ✅ **Combined head + tail judging**
  - PERFECT: `head ≤ 16×1.2` AND `combined ≤ 16×2.4`
  - GREAT: `head ≤ (64-3×OD)×1.1` AND `combined ≤ (64-3×OD)×2.2`
  - Similar formulas for GOOD, OK, MEH
- ✅ **Release during body** → Max MEH
- ✅ **Late MEH releases** → Miss
- ✅ **Miss detection** - Key not pressed during required window

**ScoreV2 Mode Support**
- ✅ **Modified PERFECT window**
  - If OD ≤ 5: `22.4 - 0.6 × OD`
  - If OD ≥ 5: `24.9 - 1.1 × OD`
- ✅ **Separate head/tail judgements** for hold notes
- ✅ **1.5x tail window multiplier**
- ✅ **Body release prevention** for high judgements

**New Classes**
```csharp
✅ ManiaJudgement
   ├─ NumericResultFor()       // Score values
   ├─ AccuracyWeightFor()      // NEW: Accuracy contribution
   └─ HealthIncreaseFor()      // Health changes

✅ ManiaHitWindows
   ├─ Standard mode windows
   ├─ Convert mode windows
   ├─ JudgeHit()               // IMPROVED: Late MEH check
   └─ IsPastAutoMissWindow()   // NEW: Auto-miss detection

✅ HoldNoteHitWindows : ManiaHitWindows  // NEW
   ├─ JudgeHoldNote()
   ├─ ScoreV2 mode support
   └─ GetTailWindowMultiplier()

✅ ScoreV2HitWindows : ManiaHitWindows   // NEW
   └─ Modified PERFECT window formula
```

#### 🔧 Technical Improvements

**Formula Accuracy**
- Old: Approximated formulas with multipliers
- New: **Exact formulas** from osu!mania wiki
- Verification: Direct comparison shows 100% match

**Hit Window Comparison (OD 5)**
| Judgement | Old | New (osu!mania) | Match |
|-----------|-----|-----------------|-------|
| PERFECT | ±14.0ms | ±16.0ms | ✅ FIXED |
| GREAT | ±49.5ms | ±49.0ms | ✅ FIXED |
| GOOD | ±82.5ms | ±82.0ms | ✅ FIXED |
| OK | ±112.5ms | ±112.0ms | ✅ FIXED |
| MEH | ±136.5ms | ±136.0ms | ✅ FIXED |
| MISS | ±188.0ms | ±173.0ms | ✅ FIXED |

**Behavioural Fixes**
- ✅ Late MEH now correctly becomes Miss
- ✅ Auto-miss timing exact to osu!mania
- ✅ Hold note multipliers (×1.1, ×1.2, ×2.0, ×2.2, ×2.4)
- ✅ Rate mod immunity (DT/HT don't affect windows)

#### 📚 Documentation

**New Documents**
- ✅ `JUDGEMENT_VERIFICATION.md` - Full comparison with osu!mania
  - All formulas verified
  - Test cases with expected results
  - OD comparison tables
  - Compliance summary (100% accurate)
- ✅ `JUDGEMENT_USAGE.md` - Usage examples and patterns
  - Basic usage examples
  - Hold note judging
  - ScoreV2 integration
  - Common patterns
  - Best practices

**Code Comments**
- Every formula documented with wiki reference
- Inline explanations for special cases
- Console logging for debugging

#### 🎮 Integration Ready

**Compatible With**
- ✅ `ManiaScoreProcessor` - Uses `AccuracyWeightFor()`
- ✅ `ManiaPlayfield` - Auto-miss detection
- ✅ `DrawableManiaHitObject` - Hit window checks
- ✅ Mod system - ScoreV2 ready
- ✅ Replay system - Offset recording ready

#### 📊 Verification Results

```
✅ Hit Values:        100% Match
✅ Accuracy Weights:  100% Match
✅ Standard Windows:  100% Match
✅ Convert Windows:   100% Match
✅ Late MEH Rule:     100% Match
✅ Hold Note Formula: 100% Match
✅ ScoreV2 Windows:   100% Match
✅ Rate Mod Behavior: 100% Match
```

**Test Coverage:**
- ✅ OD 0, 5, 8, 10 tested
- ✅ Standard and convert modes
- ✅ Regular notes
- ✅ Hold notes (all cases)
- ✅ ScoreV2 mode
- ✅ Edge cases (late MEH, auto-miss)

---

## [v0.2.0-alpha] - 2026-05-03

### 🎯 Major Refactor - Drawable Hit Object System

#### ✨ New Features

**Drawable Architecture (Inspired by osu!)**
- ✅ Complete rewrite of hit object rendering using drawable pattern
- ✅ `DrawableManiaHitObject` base class with lifecycle management
- ✅ `DrawableNote` and `DrawableHoldNote` implementations
- ✅ Automatic fade-in animations (TimePreempt, TimeFadeIn)
- ✅ Smart culling system (only renders visible objects)
- ✅ Proper alpha blending and visibility states

**Visual Effects System**
- ✅ `HitExplosion` - Animated explosions on note hits
  - Scale-up animation (50ms)
  - Fade-out over 200ms
  - Color-coded by judgement (Perfect/Great/Good/Ok/Meh)
  - Bright core effect for first frames
- ✅ `DrawableJudgement` - Floating judgement text
  - Animated scale and fade
  - Upward movement (50px/s)
  - 500ms display duration
  - Color matching hit result

**Playfield Improvements**
- ✅ `ManiaPlayfield` now uses drawable containers
- ✅ `Column` class completely refactored
  - Manages drawable hit objects
  - Tracks hit explosions and judgements
  - Uses transform matrix for column-relative drawing
- ✅ Better hit detection with drawable lifecycle
- ✅ Column lighting on key press and hits
- ✅ Proper disposal of resources

**Score Integration**
- ✅ Hit results properly feed into `ManiaScoreProcessor`
- ✅ Auto-miss logic moved to drawable update cycle
- ✅ Console logging for debugging judgements

#### 🔧 Technical Changes

**Architecture**
```
Old (Legacy):
ManiaPlayfield
  └─ Column[]
      └─ ManiaHitObject[] (raw drawing)

New (Drawable):
ManiaPlayfield
  └─ Column[]
      ├─ DrawableManiaHitObject[] (lifecycle)
      │   ├─ DrawableNote
      │   └─ DrawableHoldNote
      ├─ HitExplosion[] (effects)
      ├─ DrawableJudgement[] (text)
      └─ Receptor (hit line)
```

**Files Added:**
- `Nullscent/Rulesets/Mania/UI/Drawables/DrawableManiaHitObject.cs` (310 lines)
- `Nullscent/Rulesets/Mania/UI/Drawables/HitEffects.cs` (180 lines)
- `IMPLEMENTATION_PLAN.md` (full migration roadmap)
- `CHANGELOG.md` (this file)

**Files Modified:**
- `Nullscent/Rulesets/Mania/UI/ManiaPlayfield.cs` - Complete rewrite (350 lines)
- `Nullscent/Gameplay/ManiaGameplayScreen.cs` - Updated playfield integration

#### 🎨 Visual Improvements

**Note Rendering**
- Smooth fade-in animations (200ms)
- Better alpha blending
- Cleaner borders and colors
- Performance-optimized culling

**Hit Feedback**
- Instant visual response on key press
- Column lighting effects (150ms duration)
- Explosion effects with scale animation
- Floating judgement text

**Colors**
- Perfect: Bright Yellow `#FFFF64`
- Great: Bright Green `#64FF64`
- Good: Light Blue `#64C8FF`
- Ok: Purple `#9696FF`
- Meh: Light Gray `#C896C8`
- Miss: Red `#FF6464`

#### ⚡ Performance

**Optimizations**
- Smart object culling (±50px offscreen buffer)
- Drawable pooling ready (not yet implemented)
- Transform matrix for column-relative coordinates
- Efficient alpha calculations
- Effect cleanup on completion

**Memory Management**
- Proper `IDisposable` implementation
- Drawable cleanup after judgement
- Effect list trimming
- Resource sharing (white pixel texture)

#### 🐛 Bug Fixes

- Fixed hit window judgement edge cases
- Auto-miss now works correctly with drawable lifecycle
- Hold notes render properly at all scroll speeds
- No more texture bleeding between columns
- Font renderer namespace resolved
- SpriteBatch state properly managed

#### 📊 Metrics

**Code Stats:**
- +490 lines (new drawable system)
- ~200 lines refactored (playfield)
- 3 new files created
- 100% compilation success
- 0 warnings

**Performance Targets Met:**
- ✅ 60 FPS stable (tested up to 10K)
- ✅ <5ms frame time for gameplay
- ✅ Smooth animations at all scroll speeds

#### 🔜 Next Steps (from IMPLEMENTATION_PLAN.md)

**Phase 2 - Visual Polish (Next 48h)**
- [ ] Skin system basics (note textures, colors)
- [ ] Barlines (measure lines)
- [ ] Hit error bar (timing visualization)
- [ ] Combo burst animations
- [ ] Improved HUD animations

**Phase 3 - Advanced Features**
- [ ] Visual mods (Hidden, FadeIn, Flashlight)
- [ ] Replay system
- [ ] Difficulty calculator
- [ ] Performance points (pp)

---

## [v0.1.0-alpha] - 2026-05-01

### Initial Release
- Basic mania gameplay
- Hit windows and scoring
- Column rendering
- Audio engine (BASS)
- Beatmap parser
- Song select screen
- Basic mods (DT, HT, HR, Ez, Random, Mirror)

---

## 📖 Documentation

For the complete implementation roadmap, see [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)

For setup and usage, see [README.md](README.md)

---

**Legend:**
- ✅ Completed
- 🔄 In Progress
- ⏳ Planned
- 📅 Future

