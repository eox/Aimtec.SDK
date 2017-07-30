namespace Aimtec.SDK.Damage
{
    using System.Linq;

    /// <summary>
    ///     Class MasteryId.
    /// </summary>
    public static class MasteryId
    {
        #region Enums

        public enum Ferocity
        {
            Fury = 65,

            Sorcery = 68,

            FreshBlood = 81,

            Feast = 82,

            ExposeWeakness = 83,

            Vampirism = 97,

            NaturalTalent = 100,

            BountyHunter = 113,

            DoubleEdgedSword = 114,

            BattleTrance = 115,

            BatteringBlows = 1,

            PiercingThoughts = 4,

            WarlordsBloodlust = 17,

            FervorofBattle = 18,

            DeathfireTouch = 20
        }

        public enum Cunning
        {
            Wanderer = 65,

            Savagery = 43,

            RunicAffinity = 81,

            SecretStash = 82,

            Assassin = 83,

            Merciless = 97,

            Meditation = 98,

            GreenFathersGift = 113,

            Bandit = 114,

            DangerousGame = 115,

            Precision = 1,

            Intelligence = 2,

            StormraidersSurge = 17,

            ThunderlordsDecree = 18,

            WindspeakersBlessing = 19
        }

        public enum Resolve
        {
            Recovery = 65,

            Unyielding = 66,

            Explorer = 81,

            ToughSkin = 83,

            Siegemaster = 82,

            RunicArmor = 97,

            VeteransScars = 98,

            Insight = 113,

            Perseverance = 114,

            Fearless = 115,

            Swiftness = 1,

            LegendaryGuardian = 1,

            GraspoftheUndying = 17,

            CourageOfTheColossus = 18,

            StonebornPact = 19
        }

        #endregion

        #region Public Methods and Operators

        public static Mastery GetCunningPage(this Obj_AI_Hero hero, Cunning cunning)
        {
            return hero?.GetMastery(MasteryPage.Cunning, (uint)cunning);
        }

        public static Mastery GetFerocityPage(this Obj_AI_Hero hero, Ferocity ferocity)
        {
            return hero?.GetMastery(MasteryPage.Ferocity, (uint)ferocity);
        }

        public static Mastery GetResolvePage(this Obj_AI_Hero hero, Resolve resolve)
        {
            return hero?.GetMastery(MasteryPage.Resolve, (uint)resolve);
        }

        public static Mastery GetMastery(this Obj_AI_Hero hero, MasteryPage page, uint id)
        {
            return hero?.Masteries.FirstOrDefault(m => m != null && m.Page == page && m.Id == id);
        }

        public static bool IsUsingMastery(this Obj_AI_Hero hero, Mastery mastery)
        {
            return mastery?.Points > 0;
        }

        public static bool IsUsingMastery(this Obj_AI_Hero hero, MasteryPage page, uint mastery)
        {
            return hero?.GetMastery(page, mastery) != null;
        }

        #endregion
    }
}