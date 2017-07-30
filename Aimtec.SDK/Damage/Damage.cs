// ReSharper disable SuggestBaseTypeForParameter
namespace Aimtec.SDK.Damage
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;

    using Aimtec.SDK.Damage.JSON;
    using Aimtec.SDK.Extensions;

    /// <summary>
    ///     Class Damage.
    /// </summary>
    public static class Damage
    {
        #region Public Methods and Operators

        /// <summary>
        ///     Calculates the damage.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="damageType">Type of the damage.</param>
        /// <param name="amount">The amount.</param>
        /// <returns>System.Double.</returns>
        /// <exception cref="ArgumentOutOfRangeException">damageType - null</exception>
        public static double CalculateDamage(
            this Obj_AI_Base source,
            Obj_AI_Base target,
            DamageType damageType,
            double amount)
        {
            var damage = 0d;
            switch (damageType)
            {
                case DamageType.Magical:
                    damage = source.CalculateMagicDamage(target, amount);
                    break;
                case DamageType.Physical:
                    damage = source.CalculatePhysicalDamage(target, amount);
                    break;
                case DamageType.Mixed:
                    damage = source.CalculateMixedDamage(target, damage / 2d, damage / 2d);
                    break;
                case DamageType.True:
                    damage = Math.Max(Math.Floor(amount), 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(damageType), damageType, null);
            }

            return damage;
        }

        /// <summary>
        ///     Calculates the mixed damage.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="physicalAmount">The physical amount.</param>
        /// <param name="magicalAmount">The magical amount.</param>
        /// <returns>System.Double.</returns>
        public static double CalculateMixedDamage(
            this Obj_AI_Base source,
            Obj_AI_Base target,
            double physicalAmount,
            double magicalAmount)
        {
            return source.CalculatePhysicalDamage(target, physicalAmount) +
                   source.CalculateMagicDamage(target, magicalAmount);
        }

        /// <summary>
        ///     Gets the automatic attack damage.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <returns>System.Double.</returns>
        public static double GetAutoAttackDamage(this Obj_AI_Base source, Obj_AI_Base target)
        {
            var dmgPhysical = (double) source.TotalAttackDamage;

            var dmgMagical = 0d;
            var dmgReduce = 1d;

            var hero = source as Obj_AI_Hero;
            var targetHero = target as Obj_AI_Hero;

            if (hero != null)
            {
                var passiveDamage = DamagePassives.ComputePassiveDamages(hero, target);
                dmgPhysical += passiveDamage.PhysicalDamage;
                dmgMagical += passiveDamage.MagicalDamage;

                dmgPhysical *= passiveDamage.PhysicalDamagePercent;
                dmgMagical *= passiveDamage.MagicalDamagePercent;


                var ardentCenserBuff = hero.GetBuff("ARDENTCENSER"); // TODO: Add real buffname.
                var ardentCenserBuffCaster = (Obj_AI_Hero)ardentCenserBuff?.Caster;
                if (ardentCenserBuffCaster != null)
                {
                    dmgMagical += 19.12 + 0.88 * ardentCenserBuffCaster.Level;
                }


                if (target.GetBuffCount("braummarkcounter") == 3)
                {
                    dmgMagical += 16 + 10 * source.Level;
                }

                if (target is Obj_AI_Minion)
                {
                    var mastery = hero.GetCunningPage(MasteryId.Cunning.Savagery);
                    if (mastery != null && hero.IsUsingMastery(mastery))
                    {
                        dmgPhysical += new[] { 1, 2, 3, 4, 5 }[mastery.Points-1];
                    }

                    if (hero.HasItem(ItemId.DoransShield))
                    {
                        dmgPhysical += 5;
                    }


                    if (!hero.IsMelee &&
                        target.Team == GameObjectTeam.Neutral &&
                        Regex.IsMatch(target.Name, "SRU_RiftHerald"))
                    {
                        dmgReduce *= 0.65;
                    }

                }
            }

            if (targetHero != null)
            {
                if (!(source is Obj_AI_Turret) &&
                    targetHero.HasItem(ItemId.NinjaTabi))
                {
                    dmgReduce *= 0.9;
                }

                if (hero != null &&
                    hero.IsUsingMastery(hero.GetFerocityPage(MasteryId.Ferocity.FreshBlood)) &&
                    !hero.HasBuff("Mastery6121"))
                {
                    dmgPhysical += 10 + hero.Level;
                }

                switch (targetHero.ChampionName)
                {
                    case "Fizz":
                        dmgPhysical -= 4 + 2 * Math.Floor((targetHero.Level - 1) / 3d);
                        break;
                }
            }


            var itemDamage = DamageItems.ComputeItemDamages(source, target);
            dmgPhysical += itemDamage.PhysicalDamage;
            dmgMagical += itemDamage.MagicalDamage;

            dmgPhysical = source.CalculatePhysicalDamage(target, dmgPhysical);
            dmgMagical = source.CalculateMagicDamage(target, dmgMagical);

            switch (targetHero?.ChampionName)
            {
                case "Amumu":
                    if (targetHero.HasBuff("Tantrum"))
                    {
                        dmgPhysical -= new[] { 2, 4, 6, 8, 10 }[targetHero.SpellBook.GetSpell(SpellSlot.E).Level - 1];
                    }
                    break;
            }


            var totalDamage = Math.Floor(dmgPhysical + dmgMagical);

            var reduced = totalDamage * dmgReduce;

            var final = Math.Max(totalDamage + source.GetPassiveFlatMod(target), 0);

            return final;
        }

        /// <summary>
        ///     Gets the spell damage.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="spellSlot">The spell slot.</param>
        /// <param name="stage">The stage.</param>
        /// <returns>System.Double.</returns>
        public static double GetSpellDamage(
            this Obj_AI_Hero source,
            Obj_AI_Base target,
            SpellSlot spellSlot,
            DamageStage stage = DamageStage.Default)
        {
            if (source == null || !source.IsValid || target == null || !target.IsValid)
            {
                return 0;
            }

            if (!DamageLibrary.Damages.TryGetValue(source.ChampionName, out ChampionDamage value))
            {
                return 0;
            }

            var spellData = value.GetSlot(spellSlot)?.FirstOrDefault(e => e.Stage == stage)?.SpellData;
            if (spellData == null)
            {
                return 0;
            }

            var spellLevel =
                source.SpellBook.GetSpell(spellData.ScaleSlot != SpellSlot.Unknown ? spellData.ScaleSlot : spellSlot).Level;
            if (spellLevel == 0)
            {
                return 0;
            }

            var alreadyAdd1 = false;

            var targetHero = target as Obj_AI_Hero;
            var targetMinion = target as Obj_AI_Minion;

            var dmgBase = 0d;
            var dmgBonus = 0d;
            var dmgPassive = 0d;
            var dmgReduce = 1d;

            if (spellData.DamagesPerLvl?.Count > 0)
            {
                dmgBase = spellData.DamagesPerLvl[Math.Min(source.Level - 1, spellData.DamagesPerLvl.Count - 1)];
            }
            else if (spellData.Damages?.Count > 0)
            {
                dmgBase = spellData.Damages[Math.Min(spellLevel - 1, spellData.Damages.Count - 1)];

                if (!string.IsNullOrEmpty(spellData.ScalingBuff))
                {
                    var buffCount = (spellData.ScalingBuffTarget == DamageScalingTarget.Source ? source : target)
                        .GetBuffCount(spellData.ScalingBuff);

                    dmgBase = buffCount > 0 ? dmgBase * (buffCount + spellData.ScalingBuffOffset) : 0;
                }
            }

            if (dmgBase > 0)
            {
                if (targetMinion != null && spellData.BonusDamageOnMinion?.Count > 0)
                {
                    dmgBase += spellData.BonusDamageOnMinion[Math.Min(
                        spellLevel - 1,
                        spellData.BonusDamageOnMinion.Count - 1)];
                }

                if (spellData.IsApplyOnHit || spellData.IsModifiedDamage
                    || spellData.SpellEffectType == SpellEffectType.Single)
                {
                    alreadyAdd1 = true;
                }

                dmgBase = source.CalculateDamage(target, spellData.DamageType, dmgBase);
            }

            if (spellData.BonusDamages?.Count > 0)
            {
                foreach (var bonusDmg in spellData.BonusDamages)
                {
                    var dmg = source.GetBonusSpellDamage(target, bonusDmg, spellLevel - 1);

                    if (dmg <= 0)
                    {
                        continue;
                    }

                    if (!alreadyAdd1 && (spellData.IsModifiedDamage
                        || spellData.SpellEffectType == SpellEffectType.Single))
                    {
                        alreadyAdd1 = true;
                    }

                    dmgBonus += source.CalculateDamage(target, bonusDmg.DamageType, dmg);
                }
            }

            var totalDamage = dmgBase + dmgBonus;
            if (totalDamage > 0)
            {
                if (spellData.ScalePerCritPercent > 0)
                {
                    totalDamage *= source.Crit * 100 * spellData.ScalePerCritPercent;
                }

                if (spellData.ScalePerTargetMissHealth > 0)
                {
                    totalDamage *= (target.MaxHealth - target.Health) / target.MaxHealth
                        * spellData.ScalePerTargetMissHealth + 1;
                }

                if (target is Obj_AI_Minion && spellData.MaxDamageOnMinion?.Count > 0)
                {
                    totalDamage = Math.Min(
                        totalDamage,
                        spellData.MaxDamageOnMinion[Math.Min(spellLevel - 1, spellData.MaxDamageOnMinion.Count - 1)]);
                }

                if (spellData.IsApplyOnHit || spellData.IsModifiedDamage)
                {
                    var itemDamage = DamageItems.ComputeItemDamages(source, target);
                    dmgPassive += itemDamage.PhysicalDamage + itemDamage.MagicalDamage;
                }

                var sorcery = source.GetFerocityPage(MasteryId.Ferocity.Sorcery);
                if (sorcery != null && source.IsUsingMastery(sorcery))
                {
                    totalDamage *= 1 + new[] { 0.4, 0.8, 1.2, 1.6, 2 }[sorcery.Points - 1] / 100;
                }

                if (spellData.IsModifiedDamage)
				{
					if (targetHero != null &&
						targetHero.HasItem(ItemId.NinjaTabi))
					{
						dmgReduce *= 0.9;
					}
				}
            }

            if (spellData.IsApplyOnHit || spellData.IsModifiedDamage)
            {
                dmgPassive += source.GetPassiveFlatMod(target);
            }

            if (source.ChampionName == "Sejuani" &&
                target.HasBuff("sejuanistun"))
            {
                switch (target.Type)
                {
                    case GameObjectType.obj_AI_Hero:
                        if (source.Level < 7)
                        {
                            dmgPassive += 0.1 * target.MaxHealth;
                        }
                        else if (source.Level < 14)
                        {
                            dmgPassive += 0.15 * target.MaxHealth;
                        }
                        else
                        {
                            dmgPassive += 0.2 * target.MaxHealth;
                        }
                        break;

                    case GameObjectType.obj_AI_Minion:
                        dmgPassive += 400;
                        break;
                }
            }

            return Math.Max(Math.Floor(totalDamage * dmgReduce  + dmgPassive), 0);
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Calculates the magic damage.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="amount">The amount.</param>
        /// <returns>System.Double.</returns>
        private static double CalculateMagicDamage(this Obj_AI_Base source, Obj_AI_Base target, double amount)
        {
            if (amount < 0 || source == null || !source.IsValid || target == null || !target.IsValid)
            {
                return 0;
            }

            double value;

            if (target.SpellBlock < 0)
            {
                value = 2 - 100 / (100 - target.SpellBlock);
            }
            else if (target.SpellBlock * source.PercentMagicPenetration - source.FlatMagicPenetration < 0)
            {
                value = 1;
            }
            else
            {
                value = 100 / (100 + target.SpellBlock * source.PercentMagicPenetration - source.FlatMagicPenetration);
            }
			
            var targetHero = target as Obj_AI_Hero;
            if (target.HasBuff("cursedtouch"))
            {
                amount *= 1.1;
            }

            if (targetHero?.ChampionName == "Kassadin" &&
                targetHero.HasBuff("VoidStone"))
            {
                amount *= 0.85;
            }

            var hero = source as Obj_AI_Hero;
            var damageReductions = DamageReductions.ComputeReductions(hero, target, DamageType.Magical);
            var masteryDamage = DamageMasteries.ComputeMasteryDamages(hero, target);

            return Math.Max(
                        Math.Floor(source.GetPassivePercentMod(target, value, DamageType.Magical) * amount * damageReductions.PercentDamageReduction)
                        + masteryDamage.MagicalDamage
                        - damageReductions.FlatDamageReduction,
                   0);
        }

        /// <summary>
        ///     Calculates the physical damage.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="amount">The amount.</param>
        /// <returns>System.Double.</returns>
        private static double CalculatePhysicalDamage(this Obj_AI_Base source, Obj_AI_Base target, double amount)
        {
            if (amount < 0 || source == null || !source.IsValid || target == null || !target.IsValid)
            {
                return 0;
            }

            double armorPenetrationPercent = source.PercentArmorPenetration;
            double armorPenetrationFlat = source.PhysicalLethality * (0.6 + 0.4 * source.Level / 18);
            double bonusArmorPenetrationMod = source.PercentBonusArmorPenetration;

            switch (source.Type)
            {
                // Minions return wrong percent values.
                case GameObjectType.obj_AI_Minion:
                    armorPenetrationFlat = 0;
                    armorPenetrationPercent = 1;
                    bonusArmorPenetrationMod = 1;
                    break;

                // Turrets' Passive damage.
                case GameObjectType.obj_AI_Turret:
                    armorPenetrationFlat = 0;
                    bonusArmorPenetrationMod = 1;

                    //TODO:
                    break;
            }

            // Penetration can't reduce armor below 0.
            var armor = target.Armor;
            var bonusArmor = target.BonusArmor;

            double value;
            if (armor < 0)
            {
                value = 2 - 100 / (100 - armor);
            }
            else if (armor * armorPenetrationPercent - bonusArmor * (1 - bonusArmorPenetrationMod)
                - armorPenetrationFlat < 0)
            {
                value = 1;
            }
            else
            {
                value = 100 / (100 + armor * armorPenetrationPercent - bonusArmor * (1 - bonusArmorPenetrationMod)
                    - armorPenetrationFlat);
            }

            var hero = source as Obj_AI_Hero;
            var damageReductions = DamageReductions.ComputeReductions(hero, target, DamageType.Physical);
            var masteryDamage = DamageMasteries.ComputeMasteryDamages(hero, target);

            return Math.Max(
                        Math.Floor(source.GetPassivePercentMod(target, value, DamageType.Physical) * amount * damageReductions.PercentDamageReduction)
                        + masteryDamage.PhysicalDamage
                        - damageReductions.FlatDamageReduction,
                   0);
        }

        /// <summary>
        ///     Gets the passive flat mod.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <returns>System.Double.</returns>
        private static double GetPassiveFlatMod(this Obj_AI_Base source, Obj_AI_Base target)
        {
            var amount = 0d;

            var targetHero = target as Obj_AI_Hero;
            if (targetHero != null)
            {
                var toughSkin = targetHero.GetResolvePage(MasteryId.Resolve.ToughSkin);
                if (toughSkin != null &&
                    targetHero.IsUsingMastery(toughSkin) &&
                    (source is Obj_AI_Hero || source is Obj_AI_Minion && source.Team == GameObjectTeam.Neutral))
                {
                    amount -= 2;
                }
            }

            return amount;
        }

        /// <summary>
        ///     Gets the passive percent mod.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="amount">The amount.</param>
        /// <param name="damageType">The damageType.</param>
        /// <returns>System.Double.</returns>
        private static double GetPassivePercentMod(
            this Obj_AI_Base source,
            AttackableUnit target,
            double amount,
            // ReSharper disable once UnusedParameter.Local
            DamageType damageType)
        {
            var minion = target as Obj_AI_Minion;
            if (source is Obj_AI_Turret)
            {
                if (minion != null &&
                    (minion.UnitSkinName.Contains("Siege") || minion.UnitSkinName.Contains("Super")))
                {
                    amount *= 0.7;
                }
            }
            else if (source is Obj_AI_Hero)
            {
                if (minion != null)
                {
                    if (source.HasBuff("barontarget") &&
                        minion.UnitSkinName.Contains("SRU_Baron"))
                    {
                        amount *= 0.5;
                    }
                    else if (source.HasBuff("dragonbuff_tooltipmanager") &&
                        minion.HasBuff("s5_dragonvengeance") &&
                        minion.UnitSkinName.Contains("SRU_Dragon"))
                    {
                        /* TODO: More like broscience, not 100% consistent, the effect is "7% reduced damage for each dragon killed by your team."
                            * while this doesn't do anything else than reducing by 7% your damage for each dragon TYPE you've killed.
                            Buffs can't tell you how many dragons you've killed, nor can the enemy buff, so there's no way to tell in-game, needs some kind of property. */
                        amount *= 1 - 7 * (source.ValidActiveBuffs().Count(b => b.Name.Contains("dragonbuff")) - 1) / 100;
                    }
                }
            }

            var hero = source as Obj_AI_Hero;
            if (hero != null)
            {
                var masteryDamage = DamageMasteries.ComputeMasteryDamages(hero, target);
                amount *= masteryDamage.PercentDamage;

                var targetHero = target as Obj_AI_Hero;
                if (targetHero != null)
                {
                    var buff = hero.GetBuff("sonapassivedebuff");
                    if (buff != null)
                    {
                        var caster = buff.Caster as Obj_AI_Hero;
                        if (caster != null)
                        {
                            amount *= 1 - (0.25 + 0.04 * caster.TotalAbilityDamage / 100);
                        }
                    }

                    var masterybuff = targetHero.GetBuff("ExposeWeaknessDebuff");
                    if (masterybuff != null &&
                        hero.Team == masterybuff.Caster?.Team &&
                        masterybuff.Caster?.NetworkId != hero.NetworkId)
                    {
                        amount *= 1 + 3 / 100; // 3% Increase.
                    }

                    if (hero.MaxHealth < targetHero.MaxHealth && damageType == DamageType.Physical)
                    {
                        var healthDiff = Math.Min(targetHero.MaxHealth - hero.MaxHealth, 1000);
                        if (hero.HasItem(ItemId.LordDominiksRegards))
                        {
                            amount *= 1 + healthDiff / 5000;
                        }
                        else if (hero.HasItem(ItemId.GiantSlayer))
                        {
                            amount *= 1 + healthDiff / 10000;
                        }
                    }

                    var doubleEdgedSword = targetHero.GetFerocityPage(MasteryId.Ferocity.DoubleEdgedSword);
                    if (doubleEdgedSword != null &&
                        targetHero.IsUsingMastery(doubleEdgedSword))
                    {
                        amount *= 1 + 1.5 / 100; // 1.5% Increase.
                    }
                }
            }

            return amount;
        }

        #endregion
    }
}