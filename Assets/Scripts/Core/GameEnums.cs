namespace ParcelSort
{
    public enum DestinationColor
    {
        Red = 0,
        Blue = 1,
        Green = 2,
        Yellow = 3
    }

    public enum BeltSpeed
    {
        Stopped = 0,
        Normal = 1,
        Fast = 2
    }

    public enum ParcelSize
    {
        Small = 0,
        Large = 1,
        Oversize = 2
    }

    public enum GridDir
    {
        East = 0,
        North = 1,
        West = 2,
        South = 3
    }

    /// <summary>
    /// Round lifecycle. Prep is the pre-round install window, Result is the settle screen
    /// where rewards are paid and the profile is written to disk.
    /// </summary>
    public enum GamePhase
    {
        Prep = 0,
        Running = 1,
        Result = 2
    }

    public enum NodeType
    {
        Inlet = 0,
        Junction = 1,
        Bay = 2
    }

    /// <summary>
    /// Installable device kinds. Gate keeps value 0 so already serialized levels and
    /// profiles keep pointing at the same device.
    /// </summary>
    public enum DeviceType
    {
        Gate = 0,
        Booster = 1,
        Scanner = 2,
        AutoArm = 3
    }

    /// <summary>Where a device may legally be dropped.</summary>
    public enum InstallTargetKind
    {
        BeltSlot = 0,
        Node = 1
    }

    public enum MissionState
    {
        Idle = 0,
        Running = 1,
        Won = 2,
        Lost = 3
    }

    /// <summary>
    /// Why a round was lost. Value 2 used to be TooManyJams; jams no longer end a round, so the
    /// number is left unused rather than re-assigned, keeping older saved values unambiguous.
    /// </summary>
    public enum MissionFailReason
    {
        None = 0,
        TooManyWrong = 1,
        Timeout = 3
    }

    /// <summary>Helpers that keep every DeviceType switch in the project exhaustive.</summary>
    public static class DeviceTypes
    {
        /// <summary>Every device kind, in shop display order.</summary>
        public static readonly DeviceType[] All =
        {
            DeviceType.Gate,
            DeviceType.Booster,
            DeviceType.Scanner,
            DeviceType.AutoArm
        };

        /// <summary>Lower-case JSON key used in the level "devices" block and in profiles.</summary>
        public static string JsonKey(DeviceType device)
        {
            switch (device)
            {
                case DeviceType.Gate:
                    return "gate";
                case DeviceType.Booster:
                    return "booster";
                case DeviceType.Scanner:
                    return "scanner";
                case DeviceType.AutoArm:
                    return "autoArm";
                default:
                    return device.ToString();
            }
        }

        public static bool TryParse(string text, out DeviceType device)
        {
            device = DeviceType.Gate;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(JsonKey(All[i]), trimmed, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(All[i].ToString(), trimmed, System.StringComparison.OrdinalIgnoreCase))
                {
                    device = All[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>Player facing name shown on shop cards.</summary>
        public static string DisplayName(DeviceType device)
        {
            switch (device)
            {
                case DeviceType.Gate:
                    return "闸门";
                case DeviceType.Booster:
                    return "提速器";
                case DeviceType.Scanner:
                    return "扫描仪";
                case DeviceType.AutoArm:
                    return "自动分流臂";
                default:
                    return device.ToString();
            }
        }
    }

    public static class InstallTargets
    {
        public static string JsonKey(InstallTargetKind target)
        {
            switch (target)
            {
                case InstallTargetKind.Node:
                    return "node";
                case InstallTargetKind.BeltSlot:
                    return "beltSlot";
                default:
                    return target.ToString();
            }
        }

        public static bool TryParse(string text, out InstallTargetKind target)
        {
            target = InstallTargetKind.BeltSlot;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            if (string.Equals(trimmed, "node", System.StringComparison.OrdinalIgnoreCase))
            {
                target = InstallTargetKind.Node;
                return true;
            }

            if (string.Equals(trimmed, "beltSlot", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "belt", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "slot", System.StringComparison.OrdinalIgnoreCase))
            {
                target = InstallTargetKind.BeltSlot;
                return true;
            }

            return false;
        }
    }
}
