# 🎯 Judgement System - Usage Examples

## Quick Start Guide

This guide shows how to use Nullscent's exact osu!mania judgement implementation.

---

## 📚 Basic Usage

### 1. Creating Hit Windows

```csharp
using Nullscent.Rulesets.Mania.Judgements;

// Standard mania beatmap (OD 5)
var hitWindows = new ManiaHitWindows(overallDifficulty: 5.0, isForCurrentRuleset: true);

// Convert beatmap (osu!standard → mania, OD 7)
var convertWindows = new ManiaHitWindows(overallDifficulty: 7.0, isForCurrentRuleset: false);

// ScoreV2 mode (OD 8)
var scoreV2Windows = new ScoreV2HitWindows(overallDifficulty: 8.0);
```

---

### 2. Judging a Regular Note

```csharp
// Player hit a note
double hitTime = 1250.5;      // When player pressed key
double noteTime = 1250.0;     // When note should be hit
double offset = hitTime - noteTime;  // +0.5ms (slightly late)

// Judge the hit
HitResult result = hitWindows.JudgeHit(offset);

// Result: HitResult.Perfect (within ±16ms)
Console.WriteLine($"Judgement: {result}");  // "Judgement: Perfect"

// Get the score value
var judgement = new ManiaJudgement();
int score = judgement.NumericResultFor(result);
Console.WriteLine($"Score: {score}");  // "Score: 320"

// Get accuracy contribution
double accuracy = judgement.AccuracyWeightFor(result);
Console.WriteLine($"Accuracy: {accuracy * 100}%");  // "Accuracy: 100%"
```

---

### 3. Checking Different Offsets

```csharp
void TestJudgements(ManiaHitWindows windows)
{
    var testOffsets = new[] { -50, -16, -15, 0, 10, 49, 50, 82, 112, 136, 150 };

    foreach (var offset in testOffsets)
    {
        var result = windows.JudgeHit(offset);
        Console.WriteLine($"Offset: {offset,4}ms → {result}");
    }
}

// Output (OD 5):
// Offset:  -50ms → None (too early)
// Offset:  -16ms → Perfect
// Offset:  -15ms → Perfect
// Offset:    0ms → Perfect
// Offset:   10ms → Perfect
// Offset:   49ms → Great
// Offset:   50ms → Good
// Offset:   82ms → Good
// Offset:  112ms → Ok
// Offset:  136ms → Meh
// Offset:  150ms → Miss (late MEH becomes Miss)
```

---

## 🎹 Hold Note Judging

### Basic Hold Note

```csharp
// Create hold note hit windows
var holdWindows = new HoldNoteHitWindows(overallDifficulty: 5.0);

// Player input
double headHitTime = 1000.5;    // When head was hit
double headNoteTime = 1000.0;   // When head should be hit
double tailReleaseTime = 1502.0; // When tail was released
double tailNoteTime = 1500.0;   // When tail should be released

double headOffset = headHitTime - headNoteTime;       // +0.5ms
double tailOffset = tailReleaseTime - tailNoteTime;   // +2.0ms

// Judge the hold note
bool releasedDuringBody = false;
HitResult result = holdWindows.JudgeHoldNote(headOffset, tailOffset, releasedDuringBody);

Console.WriteLine($"Hold Note Result: {result}");
// Output: "Hold Note Result: Perfect"
// (head: 0.5 ≤ 19.2, combined: 2.5 ≤ 38.4)
```

---

### Hold Note with Early Release

```csharp
var holdWindows = new HoldNoteHitWindows(5.0);

double headOffset = 5.0;   // Hit head 5ms late
double tailOffset = 0.0;   // (irrelevant if released early)
bool releasedDuringBody = true;

HitResult result = holdWindows.JudgeHoldNote(headOffset, tailOffset, releasedDuringBody);

Console.WriteLine($"Result: {result}");
// Output: "Result: Meh" (max judgement when releasing during body)
```

---

### ScoreV2 Hold Note (Separate Head/Tail)

```csharp
var scoreV2Windows = new HoldNoteHitWindows(
    overallDifficulty: 5.0,
    isForCurrentRuleset: true,
    isScoreV2: true
);

// Judge head
double headOffset = 10.0;
HitResult headResult = scoreV2Windows.JudgeHit(headOffset);

// Judge tail (1.5x window multiplier)
double tailWindow = scoreV2Windows.WindowFor(HitResult.Great) * 
                    scoreV2Windows.GetTailWindowMultiplier();
double tailOffset = 60.0;
HitResult tailResult = tailOffset <= tailWindow ? HitResult.Great : HitResult.Good;

Console.WriteLine($"Head: {headResult}, Tail: {tailResult}");
// Output: "Head: Perfect, Tail: Great"
```

---

## 🔍 Advanced Features

### 1. Auto-Miss Detection

```csharp
var hitWindows = new ManiaHitWindows(5.0);

// Note wasn't hit, check if it should auto-miss
double noteTime = 1000.0;
double currentTime = 1120.0;
double offset = currentTime - noteTime;  // +120ms

if (hitWindows.IsPastAutoMissWindow(offset))
{
    Console.WriteLine("Auto-miss triggered");
    // Apply miss to score processor
}
```

---

### 2. Late MEH Prevention

```csharp
var hitWindows = new ManiaHitWindows(5.0);

// Test late hit in MEH window
double offset = 120.0;  // Past OK window (±112ms), in MEH range

HitResult result = hitWindows.JudgeHit(offset);

// Late MEH is impossible → becomes Miss
Console.WriteLine($"Result: {result}");  // "Result: Miss"
```

---

### 3. Window Inspection

```csharp
var hitWindows = new ManiaHitWindows(8.0);

foreach (var (result, window) in hitWindows.GetAllWindows())
{
    Console.WriteLine($"{result,-8}: ±{window:F1}ms");
}

// Output:
// Perfect : ±16.0ms
// Great   : ±40.0ms
// Good    : ±73.0ms
// Ok      : ±103.0ms
// Meh     : ±127.0ms
// Miss    : ±164.0ms
```

---

## 🎮 Integration Examples

### In Gameplay Screen

```csharp
public class ManiaGameplayScreen
{
    private ManiaHitWindows _hitWindows;
    private ManiaScoreProcessor _scoreProcessor;

    public void Initialize()
    {
        // Create hit windows based on beatmap OD
        double od = _beatmap.OverallDifficulty;
        _hitWindows = new ManiaHitWindows(od, isForCurrentRuleset: true);
    }

    public void OnKeyPress(int column, double currentTime)
    {
        // Find closest note in column
        var note = FindClosestNote(column);
        if (note == null) return;

        // Calculate offset
        double offset = currentTime - note.StartTime;

        // Judge the hit
        HitResult result = _hitWindows.JudgeHit(offset);

        if (result != HitResult.None)
        {
            // Apply to score processor
            _scoreProcessor.ApplyResult(result);

            // Mark note as judged
            note.IsJudged = true;
            note.Result = result;

            // Trigger visual effects
            SpawnHitExplosion(column, result);
            SpawnJudgementText(column, result);
        }
    }
}
```

---

### In Column Update Loop

```csharp
public class Column
{
    private ManiaHitWindows _hitWindows;

    public void Update(double currentTime, ManiaScoreProcessor scoreProcessor)
    {
        foreach (var note in _unjudgedNotes.ToList())
        {
            double offset = currentTime - note.StartTime;

            // Check for auto-miss
            if (_hitWindows.IsPastAutoMissWindow(offset))
            {
                // Note was not hit in time
                note.IsJudged = true;
                note.Result = HitResult.Miss;
                scoreProcessor.ApplyResult(HitResult.Miss);
                _unjudgedNotes.Remove(note);
            }
        }
    }
}
```

---

## 📊 Score Processor Integration

```csharp
public class ManiaScoreProcessor
{
    private readonly ManiaJudgement _judgement = new();
    private int _totalScore;
    private double _totalAccuracy;
    private int _totalJudgements;

    public void ApplyResult(HitResult result)
    {
        // Add score
        _totalScore += _judgement.NumericResultFor(result);

        // Add accuracy contribution
        _totalAccuracy += _judgement.AccuracyWeightFor(result);
        _totalJudgements++;

        // Update health
        double healthChange = _judgement.HealthIncreaseFor(result);
        Health = Math.Clamp(Health + healthChange, 0, 1);

        // Update combo
        if (result >= HitResult.Meh)
            Combo++;
        else
            Combo = 0;
    }

    public double AccuracyPercent => 
        _totalJudgements > 0 ? (_totalAccuracy / _totalJudgements) * 100 : 0;
}
```

---

## 🧪 Unit Test Examples

```csharp
[TestClass]
public class JudgementTests
{
    [TestMethod]
    public void TestPerfectWindow()
    {
        var windows = new ManiaHitWindows(5.0);

        Assert.AreEqual(HitResult.Perfect, windows.JudgeHit(0));
        Assert.AreEqual(HitResult.Perfect, windows.JudgeHit(16));
        Assert.AreEqual(HitResult.Perfect, windows.JudgeHit(-16));
        Assert.AreEqual(HitResult.Great, windows.JudgeHit(17));
    }

    [TestMethod]
    public void TestLateMehImpossible()
    {
        var windows = new ManiaHitWindows(5.0);

        // Late MEH should become Miss
        Assert.AreEqual(HitResult.Miss, windows.JudgeHit(120));
    }

    [TestMethod]
    public void TestHoldNotePerfect()
    {
        var windows = new HoldNoteHitWindows(5.0);

        double headOffset = 10.0;
        double tailOffset = 10.0;
        bool released = false;

        var result = windows.JudgeHoldNote(headOffset, tailOffset, released);

        // head: 10 ≤ 19.2 ✓
        // combined: 20 ≤ 38.4 ✓
        Assert.AreEqual(HitResult.Perfect, result);
    }
}
```

---

## 🎯 Common Patterns

### Pattern 1: Early Check Before Judgement
```csharp
double offset = currentTime - note.StartTime;

// Don't judge if too early
if (offset < -hitWindows.WindowFor(HitResult.Miss))
    return;

HitResult result = hitWindows.JudgeHit(offset);
```

---

### Pattern 2: Judgement with Feedback
```csharp
void JudgeNote(Note note, double currentTime)
{
    double offset = currentTime - note.StartTime;
    HitResult result = _hitWindows.JudgeHit(offset);

    if (result == HitResult.None)
        return;  // Too early or too late

    // Apply result
    _scoreProcessor.ApplyResult(result);

    // Log for debugging
    Console.WriteLine($"Note judged: {result} (offset: {offset:F1}ms)");

    // Visual feedback
    CreateExplosion(result);
    CreateJudgementText(result);
    PlayHitsound(result);
}
```

---

### Pattern 3: OD-Based Window Display
```csharp
void ShowHitWindows(double od)
{
    var windows = new ManiaHitWindows(od);

    Console.WriteLine($"Hit Windows (OD {od}):");
    Console.WriteLine("┌─────────┬──────────┐");
    Console.WriteLine("│ Result  │  Window  │");
    Console.WriteLine("├─────────┼──────────┤");

    foreach (var (result, window) in windows.GetAllWindows())
    {
        if (result != HitResult.None)
            Console.WriteLine($"│ {result,-7} │ ±{window,5:F1}ms │");
    }

    Console.WriteLine("└─────────┴──────────┘");
}
```

---

## 📝 Best Practices

1. **Always round offsets** before display (match osu! behavior)
2. **Use IsPastAutoMissWindow()** instead of manual checks
3. **Handle HitResult.None** (too early hits)
4. **Log judgements** during development for debugging
5. **Test with multiple OD values** (especially OD 5, 8, 10)
6. **Don't apply rate multipliers** to hit windows (mania immunity)

---

## 🚀 Performance Tips

```csharp
// ✅ GOOD: Create once per beatmap
private ManiaHitWindows _hitWindows;

public void Initialize()
{
    _hitWindows = new ManiaHitWindows(_beatmap.OD);
}

// ❌ BAD: Creating every frame
public void Update()
{
    var windows = new ManiaHitWindows(_beatmap.OD);  // Don't do this!
}
```

---

## 📚 Reference

- **Full Documentation**: `JUDGEMENT_VERIFICATION.md`
- **Implementation**: `Nullscent/Rulesets/Mania/Judgements/ManiaJudgement.cs`
- **osu!wiki**: https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu%21mania

---

**Last Updated**: 2026-05-03  
**Version**: v0.2.1-alpha
