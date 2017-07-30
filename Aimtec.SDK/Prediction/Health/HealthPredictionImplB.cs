using Aimtec.SDK.Damage;
using Aimtec.SDK.Extensions;
using Aimtec.SDK.Menu.Components;
using Aimtec.SDK.Menu.Config;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aimtec.SDK.Prediction.Health
{
    class HealthPredictionImplB : IHealthPrediction
    {
        internal Menu.Menu Config { get; set; }

        public HealthPredictionImplB()
        {
            Obj_AI_Base.OnProcessAutoAttack += Obj_AI_Base_OnProcessAutoAttack;
            GameObject.OnCreate += GameObject_OnCreate;
            GameObject.OnDestroy += GameObject_OnDestroy;
            Game.OnUpdate += Game_OnUpdate;
            Render.OnRender += Render_OnRender;
            Obj_AI_Base.OnPerformCast += Obj_AI_Base_OnPerformCast;

            Config = new Menu.Menu("HealthPred", "HealthPrediction");
            Config.Add(new MenuSeperator("seperator", "Default value is 25"));
            Config.Add(new MenuSlider("ExtraDelay", "Extra Delay", 25, 0, 250));

            AimtecMenu.Instance.Add(this.Config);
        }

        private void Obj_AI_Base_OnPerformCast(Obj_AI_Base sender, Obj_AI_BaseMissileClientDataEventArgs e)
        {
            if (sender != null && sender.IsValid && sender.IsMelee)
            {
                foreach (var value in Attacks.OutBoundAttacks)
                {
                    foreach (var a in value.Value)
                    {
                        if (a.AttackCompleted)
                        {
                            continue;
                        }

                        a.MissileDestroyed();
                    }
                }
            }
        }

        private void Render_OnRender()
        {
            if (AimtecMenu.DebugEnabled)
            {
                foreach (var m in Attacks.OutBoundAttacks)
                {
                    var attacks = m.Value;
                    foreach (var attack in attacks)
                    {
                        if (attack.Missile != null && attack.Missile.IsValid)
                        {
                            Render.Circle(attack.Missile.Position, 25, 30, System.Drawing.Color.Red);

                            var esp = attack.EstimatedPosition;

                            esp.Y = attack.Missile.Position.Y;

                            Render.Circle(esp, 25, 30, System.Drawing.Color.White);
                        }
                    }
                }
            }
        }

        public float GetPrediction(Obj_AI_Base target, int time)
        {
            float predictedDamage = 0;

            foreach (var m in Attacks.OutBoundAttacks)
            {
                var attacks = m.Value;

                foreach (var k in attacks)
                {
                    if (k.AttackCompleted || k.Target.NetworkId != target.NetworkId || Game.TickCount - k.CastTime > 2000)
                    {
                        continue;
                    }

                    if (k.META + this.Config["ExtraDelay"].Value < time)
                    {
                        predictedDamage += (float) k.Damage;
                    }
                }
            }

            return target.Health - predictedDamage;
        }

        public float GetLaneClearHealthPrediction(Obj_AI_Base target, int time)
        {
            float predictedDamage = 0;

            var rTime = time;

            foreach (var m in Attacks.OutBoundAttacks)
            {
                var attacks = m.Value;

                foreach (var k in attacks)
                {
                    if (k.Target.NetworkId != target.NetworkId || Game.TickCount - k.CastTime > rTime)
                    {
                        continue;
                    }

                    predictedDamage += (float)k.Damage;
                }
            }

            return target.Health - predictedDamage;
        }

        private int LastCleanUp { get; set; }

        private void Game_OnUpdate()
        {
            //Limit the clean up to every 3 seconds
            if (Game.TickCount - this.LastCleanUp <= 3000)
            {
                return;
            }

            //Remove attacks more than 5 seconds old 
            foreach (var kvp in Attacks.OutBoundAttacks)
            {
                kvp.Value.RemoveAll(x => x.AttackCompleted && Game.TickCount - x.CastTime > 5000);
            }

            this.LastCleanUp = Game.TickCount;
        }

        private void Obj_AI_Base_OnProcessAutoAttack(Obj_AI_Base sender, Obj_AI_BaseMissileClientDataEventArgs e)
        {
            //Ignore auto attacks happening too far away
            if (sender == null || !sender.IsValidTarget(4000, true))
            {
                return;
            }

            //Ignore local player attacks 
            if (sender.IsMe)
            {
                return;
            }

            //Only process for minion targets
            var targetM = e.Target as Obj_AI_Minion;
            if (targetM == null)
            {
                return;
            }

            Attack attack = new Attack(sender, e.Target as Obj_AI_Base, e);

            Attacks.AddAttack(attack);
        }

        private void GameObject_OnCreate(GameObject sender)
        {
            if (sender == null || !sender.IsValid)
            {
                return;
            }

            var mc = sender as MissileClient;

            if (mc == null)
            {
                return;
            }

            if (mc.SpellCaster.IsMe)
            {
                return;
            }

            var ob = Attacks.GetOutBoundAttacks(mc.SpellCaster.NetworkId);

            if (ob != null)
            {
                //Get the most recent attack processed
                var attack = ob.Where(x => x.AttackType == Attack.TypeOfAttack.Ranged && !x.AttackCompleted && x.Target.NetworkId == mc.Target.NetworkId).MaxBy(x => x.CastTime);

                if (attack != null)
                {
                    //add the missile to the attack object
                    attack.MissileCreated(mc);
                }
            }
        }

        private void GameObject_OnDestroy(GameObject sender)
        {
            if (!sender.IsValid || sender == null)
            {
                return;
            }

            var mc = sender as MissileClient;

            if (mc == null)
            {
                return;
            }

            if (mc.SpellCaster.IsMe)
            {
               return;
            }

            var ob = Attacks.GetOutBoundAttacks(mc.SpellCaster.NetworkId);

            if (ob != null)
            {
                //Get the oldest attack
                var attack = ob.Where(x => x.AttackType == Attack.TypeOfAttack.Ranged && !x.AttackCompleted && x.Target.NetworkId == mc.Target.NetworkId).MinBy(x => x.CastTime);

                if (attack != null)
                {
                    //add the missile to the attack object
                    attack.MissileDestroyed();
                }
            }
        }

        public class Attack
        {
            public Attack(Obj_AI_Base sender, Obj_AI_Base target, Obj_AI_BaseMissileClientDataEventArgs args)
            {
                this.CastTime = Game.TickCount - Game.Ping / 2;
                this.Sender = sender;
                this.StartPosition = this.Sender.ServerPosition;
                this.Target = target;
                this.MissileSpeed = args.SpellData.MissileSpeed;
                this.AnimationDelay = sender.AttackCastDelay * 1000;
                this.Damage = sender.GetAutoAttackDamage(target);

                this.TargetBR = target.BoundingRadius;
                this.SenderBR = sender.BoundingRadius;

                this.TargetID = target.NetworkId;
                this.SenderID = sender.NetworkId;

                this.AttackType = this.Sender.IsMelee ? TypeOfAttack.Melee : TypeOfAttack.Ranged;
            }

            public float TargetBR { get; set; }

            public float SenderBR { get; set; }

            public int TargetID { get; set; }

            public int SenderID { get; set; }

            public Vector3 StartPosition { get; set; }

            public Obj_AI_Base Sender { get; set; }

            public Obj_AI_Base Target { get; set; }

            public MissileClient Missile { get; set; }

            public double Damage { get; set; }

            public float Distance => StartPosition.Distance(Target) - TargetBR - SenderBR - (Missile != null && Missile.IsValid ? Missile.BoundingRadius : 25);

            public int CastTime { get; set; }

            public float MissileSpeed { get; set; }

            public float AnimationDelay { get; set; }

            public float TravelTime => this.AttackType == TypeOfAttack.Melee ? 0 : (this.Distance / this.MissileSpeed) * 1000f;

            public float TotalTimeToReach => (int)this.AnimationDelay + (int)TravelTime;

            public int EstimatedEndTime => this.CastTime + (int)TotalTimeToReach - Game.Ping / 2;

            public float DistanceTravelled => (Game.TickCount - this.CastTime - (int)this.AnimationDelay) * (this.MissileSpeed / 1000f);

            public int ETA => this.EstimatedEndTime - Game.TickCount;

            public Vector3 EstimatedPosition => this.StartPosition.Extend(this.Target.ServerPosition, (int)DistanceTravelled);


            //Missile ETA
            public int META
            {
                get
                {
                    if (this.Missile == null)
                    {
                        return this.ETA;
                    }

                    if (!this.Missile.IsValid)
                    {
                        this.AttackCompleted = true;
                        return -int.MaxValue;
                    }

                    var pos = Missile.Position;
                    var distance = pos.Distance(Missile.EndPosition) - this.Target.BoundingRadius;
                    var travelTime = (distance) / (this.MissileSpeed) * 1000;
                    return (int) travelTime - Game.Ping / 2;
                }
            }

            public bool AttackCompleted { get; set; }

            public int MissileCreationTime { get; set; }

            public void MissileCreated(MissileClient mc)
            {
                if (mc != null)
                {
                    this.MissileCreationTime = Game.TickCount - Game.Ping / 2;
                    this.Missile = mc;
                }
            }

            public int MissileDestructionTime { get; set; }

            public void MissileDestroyed()
            {
                this.MissileDestructionTime = Game.TickCount - Game.Ping / 2;
                this.AttackCompleted = true;

                if (this.Missile != null && this.Missile.IsValid)
                {
                    var dist = Missile.StartPosition.Distance(Missile.EndPosition);

                    var missilelife = this.MissileDestructionTime - this.MissileCreationTime;

                    var totallife = this.MissileDestructionTime - this.CastTime;

                    var travelTime = (dist / Missile.SpellData.MissileSpeed) * 1000;

                    var totalTime = travelTime + Missile.SpellCaster.AttackCastDelay * 1000;

                    var estimatedspeed = (dist / missilelife) * 1000;

                    var diff = this.EstimatedEndTime - this.MissileDestructionTime;

                }
            }

            public int DamageRegistrationTime { get; set; }

            public void DamageRegistered()
            {
                this.DamageRegistrationTime = Game.TickCount;
            }

            public TypeOfAttack AttackType { get; set; }

            public enum TypeOfAttack
            {
                Melee,
                Ranged
            }
        }

        class Attacks
        {
            public static Dictionary<int, List<Attack>> OutBoundAttacks { get; set; } = new Dictionary<int, List<Attack>>();

            public static void AddAttack(Attack attack)
            {
                AddOutBoundAttack(attack);
            }


            public static void AddOutBoundAttack(Attack attack)
            {
                var k = attack.Sender.NetworkId;

                if (!OutBoundAttacks.ContainsKey(k))
                {
                    OutBoundAttacks[k] = new List<Attack>();
                }

                OutBoundAttacks[k].Add(attack);
            }


            public static void RemoveOutBoundAttack(Attack attack)
            {
                var k = attack.Sender.NetworkId;
                OutBoundAttacks[k].Remove(attack);
            }

            public static List<Attack> GetOutBoundAttacks(int unitID)
            {
                List<Attack> attacks;

                OutBoundAttacks.TryGetValue(unitID, out attacks);

                return attacks;
            }
        }
    }
}
