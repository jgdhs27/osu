// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Difficulty.Preprocessing;
using osu.Game.Rulesets.Taiko.Difficulty.Skills;
using osu.Game.Rulesets.Taiko.Mods;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.Taiko.Scoring;

namespace osu.Game.Rulesets.Taiko.Difficulty
{
    public class TaikoDifficultyCalculator : DifficultyCalculator
    {
        private const double star_scaling_factor = 0.04125;

        public TaikoDifficultyCalculator(Ruleset ruleset, WorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        private DifficultyAttributes taikoCalculate(IBeatmap beatmap, Mod[] mods, double clockRate)
        {
            var skills = CreateSkills(beatmap);

            if (!beatmap.HitObjects.Any())
                return CreateDifficultyAttributes(beatmap, mods, skills, clockRate);

            var difficultyHitObjects = CreateDifficultyHitObjects(beatmap, clockRate).OrderBy(h => h.BaseObject.StartTime).ToList();

            double sectionLength = SectionLength * clockRate;

            // The first object doesn't generate a strain, so we begin with an incremented section end
            double currentSectionEnd = Math.Ceiling(beatmap.HitObjects.First().StartTime / sectionLength) * sectionLength;

            foreach (DifficultyHitObject h in difficultyHitObjects)
            {
                while (h.BaseObject.StartTime > currentSectionEnd)
                {
                    foreach (Skill s in skills)
                    {
                        s.SaveCurrentPeak();
                        s.StartNewSectionFrom(currentSectionEnd);
                    }

                    currentSectionEnd += sectionLength;
                }

                foreach (Skill s in skills)
                    s.Process(h);
            }

            // The peak strain will not be saved for the last section in the above loop
            foreach (Skill s in skills)
                s.SaveCurrentPeak();

            return CreateDifficultyAttributes(beatmap, mods, skills, clockRate);
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new TaikoDifficultyAttributes { Mods = mods, Skills = skills };

            HitWindows hitWindows = new TaikoHitWindows();
            hitWindows.SetDifficulty(beatmap.BeatmapInfo.BaseDifficulty.OverallDifficulty);

            return new TaikoDifficultyAttributes
            {
                StarRating = skills.Single().DifficultyValue() * star_scaling_factor,
                Mods = mods,
                // Todo: This int cast is temporary to achieve 1:1 results with osu!stable, and should be removed in the future
                GreatHitWindow = (int)(hitWindows.WindowFor(HitResult.Great)) / clockRate,
                MaxCombo = beatmap.HitObjects.Count(h => h is Hit),
                Skills = skills
            };
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            for (int i = 2; i < beatmap.HitObjects.Count; i++)
                yield return new TaikoDifficultyHitObject(beatmap.HitObjects[i], beatmap.HitObjects[i - 1], beatmap.HitObjects[i - 2], clockRate);
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap) => new Skill[] { new Strain() };

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new TaikoModDoubleTime(),
            new TaikoModHalfTime(),
            new TaikoModEasy(),
            new TaikoModHardRock(),
        };

        protected override DifficultyAttributes VirtualCalculate(IBeatmap beatmap, Mod[] mods, double clockRate)
            => taikoCalculate(beatmap, mods, clockRate);

    }
}
