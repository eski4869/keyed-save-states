using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JumpKing;
using JumpKing.MiscSystems.Achievements;

namespace KeyedSaveStates
{
    // The in-game attempt time is AchievementManager.m_all_time_stats._ticks
    // minus m_snapshot._ticks. Setting it moves the snapshot, the same way the
    // SaveStates mod's "Include time" does. Only the in-memory snapshot
    // changes; the game's attempt_stats.stat is left as it is.
    internal static class AttemptTimer
    {
        private const BindingFlags InstanceFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Type ManagerType = typeof(Game1).Assembly.GetType(
            "JumpKing.MiscSystems.Achievements.AchievementManager",
            true
        );

        private static readonly FieldInfo InstanceField = ManagerType.GetField(
            "instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        );

        private static readonly FieldInfo SnapshotField =
            ManagerType.GetField("m_snapshot", InstanceFields);

        private static readonly FieldInfo AllTimeStatsField =
            ManagerType.GetField("m_all_time_stats", InstanceFields);

        // Reads h, m and s, each optional, and sums them. Returns false when
        // none is present or any present one is not a non-negative number.
        public static bool TryParse(
            IReadOnlyDictionary<string, string> parameters,
            out int ticks
        )
        {
            ticks = 0;
            double seconds = 0;
            bool present = false;
            if (!TryAdd(parameters, "h", 3600, ref seconds, ref present) ||
                !TryAdd(parameters, "m", 60, ref seconds, ref present) ||
                !TryAdd(parameters, "s", 1, ref seconds, ref present) ||
                !present)
            {
                return false;
            }

            double frames = Math.Round(
                seconds / Game1.instance.TargetElapsedTime.TotalSeconds
            );
            if (frames > int.MaxValue)
            {
                return false;
            }

            ticks = (int)frames;
            return true;
        }

        public static bool HasAny(IReadOnlyDictionary<string, string> parameters)
        {
            return parameters.ContainsKey("h") ||
                parameters.ContainsKey("m") ||
                parameters.ContainsKey("s");
        }

        private static bool TryAdd(
            IReadOnlyDictionary<string, string> parameters,
            string name,
            double unitSeconds,
            ref double seconds,
            ref bool present
        )
        {
            string text;
            if (!parameters.TryGetValue(name, out text))
            {
                return true;
            }

            double value;
            if (!double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value) ||
                double.IsNaN(value) ||
                double.IsInfinity(value) ||
                value < 0)
            {
                return false;
            }

            seconds += value * unitSeconds;
            present = true;
            return true;
        }

        public static void SetTicks(int ticks)
        {
            object manager = InstanceField.GetValue(null);
            PlayerStats allTime = (PlayerStats)AllTimeStatsField.GetValue(manager);
            PlayerStats snapshot = (PlayerStats)SnapshotField.GetValue(manager);
            snapshot._ticks = allTime._ticks - ticks;
            SnapshotField.SetValue(manager, snapshot);
        }
    }
}
