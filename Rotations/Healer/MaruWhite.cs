namespace MaruRotations.Rotations.Healer
{
    [SourceCode(Path = "main/MaruRotations/Healer/MaruWhite.cs")]
    public sealed class MaruWhite : WHM_Base
    {

        #region General rotation info
        public override string GameVersion => VERSION;
        public override string RotationName => $"MaruWhite [{Type}]";
        public override CombatType Type => CombatType.PvE;
        #endregion General rotation info

        #region Configuration
        protected override IRotationConfigSet CreateConfiguration()
        {
            return base.CreateConfiguration()
                .SetBool(CombatType.PvE, "UseLilyWhenFull", true, "Use Lily at max stacks.")
                .SetBool(CombatType.PvE, "UsePreRegen", false, "Regen on Tank at 5 seconds remaining on Countdown.")
                .SetInt(CombatType.PvE, "AsylumThreshold", 2, "At how many hostiles should Asylum be used", 1, 20)
                .SetInt(CombatType.PvE, "HolyHostileThreshold", 3, "At how many hostiles should Holy be used", 1, 20);
        }
        #endregion

        #region Hooks

        public static IBaseAction RegenDefense { get; } = new BaseAction(ActionID.Regen, ActionOption.Hot)
        {
            ChoiceTarget = TargetFilter.FindAttackedTarget, 
            TargetStatus = Regen.TargetStatus,
            ActionCheck = (b, m) =>
            {

                int regenCount = PartyMembers.Count(m => m.HasStatus(true, StatusID.Regen1));

                if (b.IsJobCategory(JobRole.Tank) && b.GetHealthRatio() < 0.8)
                {
                    return true; // Prioritize tank 
                }

                return PartyMembers.Any(m => !m.IsJobCategory(JobRole.Tank) &&
                                             m.GetHealthRatio() < 0.8 &&
                                             regenCount < 2);
            }
        };


        #endregion

        #region Countdown logic
        protected override IAction CountDownAction(float remainTime)
        {
            return (remainTime < Stone.CastTime + CountDownAhead && Stone.CanUse(out var act)) ||
                   (Configs.GetBool("UsePreRegen") && remainTime <= 5 && remainTime > 3 &&
                    (RegenDefense.CanUse(out act, CanUseOption.IgnoreClippingCheck) ||
                     DivineBenison.CanUse(out act, CanUseOption.IgnoreClippingCheck))) ? act :
                   base.CountDownAction(remainTime);
        }

        #endregion

        #region GCD Logic

        protected override bool GeneralGCD(out IAction act)
        {
            bool useLilyWhenFull = Configs.GetBool("UseLilyWhenFull");

            if (PartyMembersHP.Count(health => health < 0.7) >= 3 && Medica2.CanUse(out act))
            {
                return true;
            }

            return (InCombat && RegenDefense.CanUse(out act)) ||
                    AfflatusMisery.CanUse(out act, CanUseOption.MustUse) ||
                        (useLilyWhenFull && ((Lily == 2 && LilyAfter(17)) || 
                            (Lily == 3 && BloodLily < 3)) &&
                             AfflatusMisery.EnoughLevel &&
                             ((PartyMembersAverHP < 0.7 && AfflatusRapture.CanUse(out act)) ||
                                AfflatusSolace.CanUse(out act))) ||
                                 (NumberOfAllHostilesInRange >= Configs.GetInt("HolyHostileThreshold") && Holy.CanUse(out act)) ||
                   Aero.CanUse(out act) ||
                   Stone.CanUse(out act) ||
                   Aero.CanUse(out act, CanUseOption.MustUse) ||
                   base.GeneralGCD(out act);
        }


        [RotationDesc(ActionID.AfflatusSolace, ActionID.Cure2, ActionID.Regen, ActionID.Cure)]
        protected override bool HealSingleGCD(out IAction act)
        {
            float targetHealthRatio = Regen.Target.GetHealthRatio();

            // Use Regen only if no other party member has Medica2 and the target's health is below 70%
            if (Regen.CanUse(out act) && targetHealthRatio < 0.7)
            {
                return true;
            }

            // Otherwise, prioritize other healing abilities
            return AfflatusSolace.CanUse(out act) ||
                   Cure2.CanUse(out act) ||
                   Cure.CanUse(out act) ||
                   base.HealSingleGCD(out act);
        }


        [RotationDesc(ActionID.AfflatusRapture, ActionID.Medica2, ActionID.Cure3, ActionID.Medica)]
        protected override bool HealAreaGCD(out IAction act)
        {
            int hasMedica2 = PartyMembers.Count(n => n.HasStatus(true, StatusID.Medica2));
            float averagePartyHP = PartyMembersAverHP / PartyMembers.Count();
            bool useLilyWhenFull = Configs.GetBool("UseLilyWhenFull");

            if ((averagePartyHP < 0.8 &&
                hasMedica2 > PartyMembers.Count() / 2 && Medica2.CanUse(out act)))
            {
                return true;
            }
            // Use Afflatus Rapture when lilies are available and two or more party members need healing
            else if (useLilyWhenFull && ((Lily >= 2 && LilyAfter(17)) ||
                        (Lily == 3 && BloodLily < 3)) &&  AfflatusRapture.CanUse(out act))
            {
                return true;
            }
            else
            {
                // Fall back to other healing abilities when conditions for Medica2 are not met
                return  Cure3.CanUse(out act) ||
                            Medica.CanUse(out act) ||
                                base.HealAreaGCD(out act);
            }
        }

        #endregion

        #region oGCD Logic
        protected override bool AttackAbility(out IAction act) =>
            PresenceOfMind.CanUse(out act) ||
            (HasHostilesInMaxRange && Assize.CanUse(out act, CanUseOption.MustUse)) ||
            base.AttackAbility(out act);


        [RotationDesc(ActionID.Benediction, ActionID.Asylum, ActionID.DivineBenison, ActionID.Tetragrammaton)]
        protected override bool HealSingleAbility(out IAction act) =>
            (Benediction.CanUse(out act) && Benediction.Target.GetHealthRatio() < 0.5) ||
            (!IsMoving && NumberOfAllHostilesInRange >= Configs.GetInt("AsylumThreshold") && Asylum.CanUse(out act)) ||
            DivineBenison.CanUse(out act) ||
            Tetragrammaton.CanUse(out act) ||
            base.HealSingleAbility(out act);


        [RotationDesc(ActionID.Asylum)]
        protected override bool HealAreaAbility(out IAction act) =>
            (!IsMoving && NumberOfAllHostilesInRange >= Configs.GetInt("AsylumThreshold") && Asylum.CanUse(out act, CanUseOption.IgnoreClippingCheck)) ||
            base.HealAreaAbility(out act);


        [RotationDesc(ActionID.DivineBenison, ActionID.Aquaveil)]
        protected override bool DefenseSingleAbility(out IAction act) =>
            DivineBenison.CanUse(out act) ||
            Aquaveil.CanUse(out act) ||
            base.DefenseSingleAbility(out act);


        [RotationDesc(ActionID.Temperance, ActionID.LiturgyOfTheBell)]
        protected override bool DefenseAreaAbility(out IAction act) =>
            Temperance.CanUse(out act) ||
            LiturgyOfTheBell.CanUse(out act) ||
            base.DefenseAreaAbility(out act);


        protected override bool EmergencyAbility(IAction nextGCD, out IAction act) =>
            ((nextGCD is IBaseAction action && action.MPNeed >= 999 && ThinAir.CanUse(out act)) ||
            (nextGCD.IsTheSameTo(true, AfflatusRapture, Medica, Medica2, Cure3) && PlenaryIndulgence.CanUse(out act))) ||
            base.EmergencyAbility(nextGCD, out act);

        #endregion

    }
}