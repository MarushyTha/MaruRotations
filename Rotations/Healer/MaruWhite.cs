using System;

namespace MaruRotations.Rotations.Healer
{
    [Rotation("MaruRotations", CombatType.PvE, GameVersion = "6.58")]
    [SourceCode(Path = "main/MaruRotations/Rotations/Healer/MaruWhite.cs")]
    public sealed class MaruWhite : WhiteMageRotation
    {
        #region Configuration

        [RotationConfig(CombatType.PvE, Name = "Use Lily at max stacks.")]
        public bool UseLilyWhenFull { get; set; } = true;

        [RotationConfig(CombatType.PvE, Name = "Regen on Tank at 5 seconds remaining on Countdown.")]
        public bool UsePreRegen { get; set; } = true;

        [RotationConfig(CombatType.PvE, Name = "At how many hostiles should Asylum be used.")]
        public int AsylumThreshold { get; set; } = 2;

        [RotationConfig(CombatType.PvE, Name = "At how many hostiles should Holy be used.")]
        public int HolyHostileThreshold { get; set; } = 3;

        #endregion

        #region Hooks

        public MaruWhite()
        {
            AfflatusRapturePvE.Setting.RotationCheck = () => BloodLily < 3 && UseLilyWhenFull;
            AfflatusSolacePvE.Setting.RotationCheck = () => BloodLily < 3 && UseLilyWhenFull;
            AsylumPvE.Setting.RotationCheck = () => AsylumThreshold > 0;
            HolyPvE.Setting.RotationCheck = () => HolyHostileThreshold > 0;
        }

        #endregion

        #region Helpers

        // Helper method to check if Lily stacks are nearly full
        private static bool IsLiliesNearlyFull() => Lily == 2 && LilyTime > 13;

        // Helper method to check if Lily stacks are full but Blood Lily is not
        private static bool IsLiliesFullNoBlood() => Lily == 3 && BloodLily < 3;

        // Method to determine whether to use Lily ability
        private bool UseLily(out IAction? act, bool useAoE)
        {

            // Use Lily based on AoE flag
            if (useAoE && AfflatusRapturePvE.CanUse(out act, skipAoeCheck: true))
                return true;
            else if (AfflatusSolacePvE.CanUse(out act))
                return true;

            // If no Lily ability is usable, assign a default null value
            act = null;
            return false;
        }

        private bool ShouldUseMedicaII(out IAction? act)
        {
            // Calculate the threshold for Medica II based on the number of party members close by
            int maxRange = 20;
            int partyMembersWithinRange = PartyMembers.Count(member => member.DistanceToPlayer() < maxRange);
            int medicaThreshold = partyMembersWithinRange / 2; // Dynamic threshold based on party size

            // Check if Medica II can be used
            bool canUseMedicaII = MedicaIiPvE.CanUse(out act, skipClippingCheck: true);

            // Check if party health is below the threshold for Medica II
            bool isPartyHealthBelowThreshold = PartyMembersAverHP < 0.75f;

            // Check if there are enough hostiles for Medica II
            bool enoughHostilesForMedicaII = NumberOfAllHostilesInRange >= medicaThreshold;

            // Check if there is a single hostile target (boss)
            bool isBossTarget = HostileTarget.IsBossFromIcon();

            // Use Medica II if:
            // 1. Party health is below threshold and enough hostiles are present and Medica II is usable
            // OR 2. We're fighting a boss (single target) and Medica II is usable
            return (isPartyHealthBelowThreshold && enoughHostilesForMedicaII || isBossTarget) && canUseMedicaII;
        }

        // Method to determine whether to use Holy ability
        private bool UseHoly(out IAction? act)
        {
            // Check if Holy can be used and if enough hostiles are present
            bool canUseHoly = HolyPvE.CanUse(out act);
            bool shouldUseHoly = NumberOfAllHostilesInRange >= HolyHostileThreshold && canUseHoly;
            return shouldUseHoly;
        }

        #endregion

        #region Countdown logic

        // Override method for countdown actions
        protected override IAction? CountDownAction(float remainTime)
        {
            IAction? act;

            // If the remaining time is less than the cast time of Stone plus the countdown ahead time
            if (remainTime < StonePvE.Info.CastTime + CountDownAhead)
            {
                // Try to use Stone if it can be used
                if (StonePvE.CanUse(out act))
                    return act;
            }

            // If using pre-regen is enabled and there are 5 seconds or less remaining
            if (UsePreRegen && remainTime <= 5 && remainTime > 3)
            {
                // Try to use Regen or Divine Benison if they can be used
                if (RegenPvE.CanUse(out act))
                    return act;
                if (DivineBenisonPvE.CanUse(out act))
                    return act;
            }

            return base.CountDownAction(remainTime);
        }

        #endregion

        #region GCD Logic

        protected override bool GeneralGCD(out IAction? act)
        {
            bool shouldUseAfflatusSolace = BloodLily < 3 || (BloodLily >= 3 && LilyTime < 13);

            if (shouldUseAfflatusSolace)
            {
                // Prioritize healing party members with Afflatus Solace
                foreach (var member in PartyMembers)
                {
                    // Check if the party member needs healing
                    if (member.GetHealthRatio() < 0.7 && AfflatusSolacePvE.CanUse(out act, member))
                        return true;
                }
            }

            // Check for AoE Lily usage
            if ((IsLiliesNearlyFull() || IsLiliesFullNoBlood()) && UseLily(out act, true))
                return true;

            // Check if Medica II should be used
            if (ShouldUseMedicaII(out act))
            {
                return true;
            }

            // Prioritize healing with Cure II if party health is low
            if (PartyMembersAverHP < 0.7 && CureIiPvE.CanUse(out act))
                return true;

            // Check for single-target Lily usage (Afflatus Solace)
            if (UseLily(out act, false))
                return true;

            // AoE damage (Holy)
            if (HolyPvE.CanUse(out _))
            {
                if (UseHoly(out act))
                    return true;
            }

            // Single-target DoT (Aero)
            if (AeroPvE.CanUse(out act))
                return true;

            // Single-target damage (Stone)
            if (StonePvE.CanUse(out act))
                return true;

            // Maintain DoT (Aero) even if already applied 
            if (AeroPvE.CanUse(out act, skipStatusProvideCheck: true))
                return true;

            // If no action is taken, call the base method
            return base.GeneralGCD(out act);
        }

        [RotationDesc(ActionID.AfflatusSolacePvE, ActionID.CureIiPvE, ActionID.RegenPvE, ActionID.CurePvE)]
        protected override bool HealSingleGCD(out IAction? act)
        {
            float targetHealthRatio = (float)(RegenPvE.Target.Target?.GetHealthRatio());

            // Prioritize Regen if conditions are met
            if (RegenPvE.CanUse(out act) && targetHealthRatio < 0.7)
            {
                return true;
            }

            // Prioritize other healing abilities 
            return AfflatusSolacePvE.CanUse(out act) ||
                   CureIiPvE.CanUse(out act) ||
                   CurePvE.CanUse(out act) ||
                   base.HealSingleGCD(out act);
        }

        [RotationDesc(ActionID.AfflatusRapturePvE, ActionID.MedicaIiPvE, ActionID.CureIiiPvE, ActionID.MedicaPvE)]
        protected override bool HealAreaGCD(out IAction? act)
        {

            // Check for AoE Lily usage
            if ((Lily == 2 && LilyTime > 13 || Lily == 3 && BloodLily < 3) && UseLily(out act, true))
            {
                return true;
            }

            // If neither Medica II nor AoE Lily is usable, use other healing spells
            return CureIiiPvE.CanUse(out act) ||
                   MedicaPvE.CanUse(out act) ||
                   base.HealAreaGCD(out act);
        }

        #endregion

        #region oGCD Logic

        [RotationDesc(ActionID.PresenceOfMindPvE, ActionID.AssizePvE)]
        protected override bool AttackAbility(out IAction? act) =>
            InCombat && (PresenceOfMindPvE.CanUse(out act) || (HasHostilesInMaxRange && AssizePvE.CanUse(out act, CanUseOption.SkipClippingCheck))) ||
            base.AttackAbility(out act);

        [RotationDesc(ActionID.BenedictionPvE, ActionID.AsylumPvE, ActionID.DivineBenisonPvE, ActionID.TetragrammatonPvE)]
        protected override bool HealSingleAbility(out IAction? act) =>
            (BenedictionPvE.CanUse(out act) && BenedictionPvE.Target.Target?.GetHealthRatio() < 0.5d) ||
            (!IsMoving && NumberOfAllHostilesInRange >= AsylumThreshold && AsylumPvE.CanUse(out act, CanUseOption.SkipClippingCheck)) ||
            DivineBenisonPvE.CanUse(out act) ||
            TetragrammatonPvE.CanUse(out act) ||
            base.HealSingleAbility(out act);

        [RotationDesc(ActionID.AsylumPvE)]
        protected override bool HealAreaAbility(out IAction? act)
        {
            if (!IsMoving && NumberOfAllHostilesInRange >= AsylumThreshold && AsylumPvE.CanUse(out act, CanUseOption.SkipClippingCheck))
            {
                return true;
            }
            else
            {
                return base.HealAreaAbility(out act);
            }
        }

        [RotationDesc(ActionID.DivineBenisonPvE, ActionID.AquaveilPvE)]
        protected override bool DefenseSingleAbility(out IAction? act) =>
            DivineBenisonPvE.CanUse(out act) && DivineBenisonPvE.Cooldown.WillHaveOneCharge(15) ||
            AquaveilPvE.CanUse(out act) && AquaveilPvE.Cooldown.WillHaveOneCharge(52) ||
            base.DefenseSingleAbility(out act);

        [RotationDesc(ActionID.TemperancePvE, ActionID.LiturgyOfTheBellPvE)]
        protected override bool DefenseAreaAbility(out IAction? act)
        {
            // Check if Temperance and LiturgyOfTheBell are on cooldown and have enough charges
            bool temperanceOnCooldown = TemperancePvE.Cooldown.IsCoolingDown && !TemperancePvE.Cooldown.WillHaveOneCharge(100);
            bool liturgyOnCooldown = LiturgyOfTheBellPvE.Cooldown.IsCoolingDown && !LiturgyOfTheBellPvE.Cooldown.WillHaveOneCharge(160);

            // If either ability is on cooldown, return false
            if (temperanceOnCooldown || liturgyOnCooldown)
            {
                act = null; // Assign a default null value here
                return false;
            }

            // Try to use Temperance first
            if (TemperancePvE.CanUse(out act))
            {
                return true;
            }

            // If Temperance cannot be used, try to use LiturgyOfTheBell
            if (LiturgyOfTheBellPvE.CanUse(out act, skipAoeCheck: true))
            {
                return true;
            }

            // If neither ability can be used, call the base method
            return base.DefenseAreaAbility(out act);
        }

        protected override bool EmergencyAbility(IAction nextGCD, out IAction? act)
        {
            if (nextGCD is IBaseAction action && action.Info.MPNeed >= 1000 && ThinAirPvE.CanUse(out act))
                return true;
            if (nextGCD.IsTheSameTo(true, AfflatusRapturePvE, MedicaPvE, MedicaIiPvE, CureIiiPvE) &&
                (MergedStatus.HasFlag(AutoStatus.HealAreaSpell) || MergedStatus.HasFlag(AutoStatus.HealSingleSpell)) &&
                PlenaryIndulgencePvE.CanUse(out act))
                return true;
            return base.EmergencyAbility(nextGCD, out act);
        }

        #endregion
    }
}
