#nullable enable

namespace Nullscent.Rulesets.Mania.Mods
{
    public abstract class ManiaMod
    {
        public abstract string Name { get; }
        public abstract string Acronym { get; }
        public abstract string Description { get; }
        public virtual double ScoreMultiplier => 1.0;
        public virtual bool Ranked => true;

        public virtual void Apply() { }
    }

    public class ManiaModEasy : ManiaMod
    {
        public override string Name => "Easy";
        public override string Acronym => "EZ";
        public override string Description => "Reduces overall difficulty";
        public override double ScoreMultiplier => 0.5;
    }

    public class ManiaModNoFail : ManiaMod
    {
        public override string Name => "No Fail";
        public override string Acronym => "NF";
        public override string Description => "You can't fail";
        public override double ScoreMultiplier => 0.5;
    }

    public class ManiaModHalfTime : ManiaMod
    {
        public override string Name => "Half Time";
        public override string Acronym => "HT";
        public override string Description => "0.75x playback speed";
        public override double ScoreMultiplier => 0.3;
        public double SpeedMultiplier => 0.75;
    }

    public class ManiaModHardRock : ManiaMod
    {
        public override string Name => "Hard Rock";
        public override string Acronym => "HR";
        public override string Description => "Increases overall difficulty";
        public override double ScoreMultiplier => 1.06;
    }

    public class ManiaModDoubleTime : ManiaMod
    {
        public override string Name => "Double Time";
        public override string Acronym => "DT";
        public override string Description => "1.5x playback speed";
        public override double ScoreMultiplier => 1.12;
        public double SpeedMultiplier => 1.5;
    }

    public class ManiaModHidden : ManiaMod
    {
        public override string Name => "Hidden";
        public override string Acronym => "HD";
        public override string Description => "Notes fade out before reaching the judgement line";
        public override double ScoreMultiplier => 1.06;
    }

    public class ManiaModFlashlight : ManiaMod
    {
        public override string Name => "Flashlight";
        public override string Acronym => "FL";
        public override string Description => "Restricted view area";
        public override double ScoreMultiplier => 1.12;
    }

    public class ManiaModRandom : ManiaMod
    {
        public override string Name => "Random";
        public override string Acronym => "RD";
        public override string Description => "Shuffle the columns";
        public override double ScoreMultiplier => 1.0;
    }

    public class ManiaModMirror : ManiaMod
    {
        public override string Name => "Mirror";
        public override string Acronym => "MR";
        public override string Description => "Flip columns horizontally";
        public override double ScoreMultiplier => 1.0;
    }

    public abstract class ManiaModKey : ManiaMod
    {
        public abstract int KeyCount { get; }
        public override double ScoreMultiplier => 1.0;
    }

    public class ManiaModKey1 : ManiaModKey
    {
        public override string Name => "1K";
        public override string Acronym => "1K";
        public override string Description => "1 key mode";
        public override int KeyCount => 1;
    }

    public class ManiaModKey4 : ManiaModKey
    {
        public override string Name => "4K";
        public override string Acronym => "4K";
        public override string Description => "4 key mode";
        public override int KeyCount => 4;
    }

    public class ManiaModKey7 : ManiaModKey
    {
        public override string Name => "7K";
        public override string Acronym => "7K";
        public override string Description => "7 key mode";
        public override int KeyCount => 7;
    }
}
