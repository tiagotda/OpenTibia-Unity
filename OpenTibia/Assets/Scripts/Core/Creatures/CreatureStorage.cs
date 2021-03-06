﻿using System.Collections.Generic;
using System.Linq;

namespace OpenTibiaUnity.Core.Creatures
{
    internal class CreatureStorage {
        internal class OpponentsChangeEvent : UnityEngine.Events.UnityEvent<List<Creature>> {}

        private int m_CreatureCount = 0;
        private OpponentStates m_OpponentState = OpponentStates.NoAction;
        private List<Creature> m_Opponents = new List<Creature>();
        private int m_CreatureIndex = 0;
        private readonly int m_MaxCreaturesCount = 1300;
        private List<Creature> m_Creatures = new List<Creature>();

        internal Creature Aim { get; set; } = null;
        internal Creature AttackTarget { get; set; } = null;
        internal Creature FollowTarget { get; set; } = null;
        List<Creature> Trappers { get; set; } = null;
        internal Player Player { get; set; }

        internal OpponentsChangeEvent onOpponentsRefreshed {
            get; private set;
        } = new OpponentsChangeEvent();

        internal OpponentsChangeEvent onOpponentsRebuilt {
            get; private set;
        } = new OpponentsChangeEvent();

        internal CreatureStorage() {
            Player = new Player(0);
        }

        internal void SetAim(Creature aim) {
            if (Aim != aim) {
                var creature = Aim;
                Aim = aim;
                UpdateExtendedMark(creature);
                UpdateExtendedMark(Aim);
            }
        }

        internal Creature GetNextAttackTarget(int step) {
            step = step < 0 ? -1 : 1;

            int total = m_Opponents.Count;
            if (total < 1)
                return null;

            int attackedIndex = AttackTarget ? m_Opponents.FindIndex((x) => x == AttackTarget) : -1;
            
            for (int i = 0; i < total; i++) {
                attackedIndex += step;
                if (attackedIndex >= total)
                    attackedIndex = 0;

                if (attackedIndex < 0)
                    attackedIndex = total - 1;

                var creature = m_Opponents[attackedIndex];
                if (creature.Type != CreatureType.NPC)
                    return creature;
            }

            return null;
        }
        
        internal void MarkAllOpponentsVisible(bool value) {
            foreach (var opponent in m_Opponents) {
                opponent.Visible = value;
            }

            if (m_Opponents.Count > 0)
                InvalidateOpponents();
        }

        internal Creature ReplaceCreature(Creature creature, uint id = 0) {
            if (!creature)
                throw new System.ArgumentException("CreatureStorage.replaceCreature: Invalid creature.");
            
            if (id != 0)
                RemoveCreature(id);

            if (m_CreatureCount >= m_MaxCreaturesCount)
                throw new System.ArgumentException("CreatureStorage.replaceCreature: No space left to append " + creature.ID);
            
            int index = 0;
            int lastIndex = m_CreatureCount - 1;
            while (index <= lastIndex) {
                int tmpIndex = index + lastIndex >> 1;
                var foundCreature = m_Creatures[tmpIndex];
                if (foundCreature.ID < creature.ID)
                    index = tmpIndex + 1;
                else if (foundCreature.ID > creature.ID)
                    lastIndex = tmpIndex - 1;
                else
                    return foundCreature;
            }

            creature.KnownSince = ++m_CreatureIndex;
            m_Creatures.Insert(index, creature);
            m_CreatureCount++;
            m_OpponentState = OpponentStates.Rebuild;
            return creature;
        }

        internal void ToggleFollowTarget(Creature follow, bool send) {
            if (follow == Player) {
                throw new System.ArgumentException("CreatureStorage.ToggleFollowTarget: Cannot follow player.");
            }

            var creature = FollowTarget;
            if (creature != follow)
                FollowTarget = follow;
            else
                FollowTarget = null;
            
            if (send) {
                var protocolGame = OpenTibiaUnity.ProtocolGame;
                if (protocolGame)
                    protocolGame.SendFollow(FollowTarget ? FollowTarget.ID : 0);
            }

            UpdateExtendedMark(creature);
            UpdateExtendedMark(FollowTarget);

            if (AttackTarget) {
                creature = AttackTarget;
                AttackTarget = null;
                UpdateExtendedMark(creature);
            }
        }

        internal Creature GetCreatureByName(string name) {
            return m_Creatures.Find((x) => name == x.Name);
        }

        internal void RefreshOpponents() {
            switch (m_OpponentState) {
                case OpponentStates.NoAction:
                    break;

                case OpponentStates.Refresh:
                case OpponentStates.Rebuild: {
                    m_Opponents.Clear();
                    for (int i = 0; i < m_CreatureCount; i++) {
                        Creature creature = m_Creatures[i];
                        if (IsOpponent(creature, true))
                            m_Opponents.Add(creature);
                    }
                    
                    m_Opponents.Sort(OpponentComparator);
                    onOpponentsRebuilt.Invoke(m_Opponents);
                    break;
                }
            }
            
            m_OpponentState = OpponentStates.NoAction;
        }

        protected int OpponentComparator(Creature a, Creature b) {
            if (a == null || b == null)
                return 0;
            
            var pos = 0;
            var sortType = OpenTibiaUnity.OptionStorage.OpponentSort;

            bool desc = false;
            if (sortType == OpponentSortTypes.SortDistanceDesc || sortType == OpponentSortTypes.SortHitpointsDesc || sortType == OpponentSortTypes.SortKnownSinceDesc || sortType == OpponentSortTypes.SortNameDesc)
                desc = true;

            switch (sortType) {
                case OpponentSortTypes.SortDistanceAsc:
                case OpponentSortTypes.SortDistanceDesc:
                    var myPosition = Player.Position;
                    var d1 = System.Math.Max(System.Math.Abs(myPosition.x - a.Position.x), System.Math.Abs(myPosition.y - a.Position.y));
                    var d2 = System.Math.Max(System.Math.Abs(myPosition.x - b.Position.x), System.Math.Abs(myPosition.y - b.Position.y));
                    if (d1 < d2)
                        pos = -1;
                    else if (d1 > d2)
                        pos = 1;

                    break;

                case OpponentSortTypes.SortHitpointsAsc:
                case OpponentSortTypes.SortHitpointsDesc:
                    if (a.HealthPercent < b.HealthPercent)
                        pos = -1;
                    else if (a.HealthPercent > b.HealthPercent)
                        pos = 1;

                    break;

                case OpponentSortTypes.SortNameAsc:
                case OpponentSortTypes.SortNameDesc:
                    pos = a.Name.CompareTo(b.Name);
                    break;

                case OpponentSortTypes.SortKnownSinceAsc:
                case OpponentSortTypes.SortKnownSinceDesc:
                    break;
            }

            if (pos == 0) {
                if (a.KnownSince < b.KnownSince)
                    pos = -1;
                else if (a.KnownSince > b.KnownSince)
                    pos = 1;
                else
                    return 0;
            }

            return pos * (desc ? -1 : 1);
        }

        internal void SetFollowTarget(Creature follow, bool send) {
            if (follow == Player) {
                throw new System.ArgumentException("CreatureStorage.ToggleFollowTarget: Cannot follow player.");
            }

            Creature creature = FollowTarget;
            if (creature != follow) {
                FollowTarget = follow;

                if (send) {
                    var protocolGame = OpenTibiaUnity.ProtocolGame;
                    if (protocolGame)
                        protocolGame.SendFollow(FollowTarget ? FollowTarget.ID : 0);
                }

                UpdateExtendedMark(creature);
                UpdateExtendedMark(FollowTarget);
            }

            if (AttackTarget) {
                creature = AttackTarget;
                AttackTarget = null;
                UpdateExtendedMark(creature);
            }
        }
        
        protected void UpdateExtendedMark(Creature creature) {
            if (!creature)
                return;

            // aim-attack: white outline & red border
            // aim-follow: white outline & green border
            // aim: white outline
            // attack: red border
            // follow: green border

            Appearances.Marks marks = creature.Marks;
            if (creature == Aim) {
                if (creature == AttackTarget) {
                    marks.SetMark(MarkType.ClientMapWindow, Appearances.Marks.MarkAimAttack);
                    marks.SetMark(MarkType.ClientBattleList, Appearances.Marks.MarkAimAttack);
                } else if (creature == FollowTarget) {
                    marks.SetMark(MarkType.ClientMapWindow, Appearances.Marks.MarkAimFollow);
                    marks.SetMark(MarkType.ClientBattleList, Appearances.Marks.MarkAimFollow);
                } else {
                    marks.SetMark(MarkType.ClientMapWindow, Appearances.Marks.MarkAim);
                    marks.SetMark(MarkType.ClientBattleList, Appearances.Marks.MarkAim);
                }
            } else if (creature == AttackTarget) {
                marks.SetMark(MarkType.ClientMapWindow, Appearances.Marks.MarkAttack);
                marks.SetMark(MarkType.ClientBattleList, Appearances.Marks.MarkAttack);
            } else if (creature == FollowTarget) {
                marks.SetMark(MarkType.ClientMapWindow, Appearances.Marks.MarkFollow);
                marks.SetMark(MarkType.ClientBattleList, Appearances.Marks.MarkFollow);
            } else {
                marks.SetMark(MarkType.ClientMapWindow, Appearances.Marks.MarkUnmarked);
                marks.SetMark(MarkType.ClientBattleList, Appearances.Marks.MarkUnmarked);
            }
        }

        internal void SetTrappers(List<Creature> trappers) {
            int index = Trappers != null ? Trappers.Count : -1;
            while (index >= 0) {
                if (Trappers[index] != null)
                    Trappers[index].Trapper = false;
                index--;
            }

            Trappers = trappers;
            index = Trappers != null ? Trappers.Count : -1;
            while (index >= 0) {
                if (Trappers[index] != null)
                    Trappers[index].Trapper = true;
                index--;
            }
        }

        internal Creature GetCreature(uint id) {
            int index = 0;
            int lastIndex = m_CreatureCount - 1;
            while (index <= lastIndex) {
                int tmpIndex = index + lastIndex >> 1;
                Creature foundCreature = m_Creatures[tmpIndex];
                if (foundCreature.ID < id)
                    index = tmpIndex + 1;
                else if (foundCreature.ID > id)
                    lastIndex = tmpIndex - 1;
                else
                    return foundCreature;
            }

            return null;
        }

        internal void InvalidateOpponents() {
            if (m_OpponentState < OpponentStates.Refresh)
                m_OpponentState = OpponentStates.Refresh;
        }

        internal void MarkOpponentVisible(object param, bool visible) {
            Creature creature;
            if (param is Creature) {
                creature = param as Creature;
            } else if (param is Appearances.ObjectInstance @object) {
                creature = GetCreature(@object.Data);
            } else if (param is uint || param is int) {
                creature = GetCreature((uint)param);
            } else {
                throw new System.ArgumentException("CreatureStorage.MarkOpponentVisible: Invalid overload.");
            }

            if (creature) {
                creature.Visible = visible;
                InvalidateOpponents();
            }
        }

        internal bool IsOpponent(Creature creature) {
            return IsOpponent(creature, false);
        }

        protected bool IsOpponent(Creature creature, bool deepCheck) {
            if (!creature || creature is Player)
                return false;

            var creaturePosition = creature.Position;
            var myPosition = Player.Position;

            if (creaturePosition.z != myPosition.z || System.Math.Abs(creaturePosition.x - myPosition.x) > Constants.MapWidth / 2 || System.Math.Abs(creaturePosition.y - myPosition.y) > Constants.MapHeight / 2)
                return false;

            var filter = OpenTibiaUnity.OptionStorage.OpponentFilter;
            if (!deepCheck || filter == OpponentFilters.None)
                return true;

            if ((filter & OpponentFilters.Players) > 0 && creature.Type == CreatureType.Player)
                return false;

            if ((filter & OpponentFilters.NPCs) > 0 && creature.Type == CreatureType.NPC)
                return false;

            if ((filter & OpponentFilters.Monsters) > 0 && creature.Type == CreatureType.Monster)
                return false;

            if ((filter & OpponentFilters.NonSkulled) > 0 && creature.Type == CreatureType.Player && creature.PKFlag == PKFlag.None)
                return false;

            if ((filter & OpponentFilters.Party) > 0 && creature.PartyFlag != PartyFlag.None)
                return false;

            if ((filter & OpponentFilters.Summons) > 0 && creature.SummonTypeFlag != SummonTypeFlags.None)
                return false;
            
            return true;
        }

        internal void Reset(bool resetPlayer = true) {
            if (resetPlayer)
                Player.Reset();

            m_Creatures.ForEach(creature => creature.Reset());

            m_Creatures.Clear();
            m_CreatureCount = 0;
            m_CreatureIndex = 0;

            if (!resetPlayer)
                ReplaceCreature(Player);

            m_Opponents.Clear();
            m_OpponentState = OpponentStates.NoAction;
            Aim = null;
            AttackTarget = null;
            FollowTarget = null;
            Trappers = null;
        }

        internal void RemoveCreature(uint id) {
            int currentIndex = 0;
            int lastIndex = m_CreatureCount - 1;

            int foundIndex = -1;
            Creature foundCreature = null;
            while (currentIndex <= lastIndex) {
                int tmpIndex = currentIndex + lastIndex >> 1;
                foundCreature = m_Creatures[tmpIndex];
                if (foundCreature.ID < id) {
                    currentIndex = tmpIndex + 1;
                } else if (foundCreature.ID > id) {
                    lastIndex = tmpIndex - 1;
                } else {
                    foundIndex = tmpIndex;
                    break;
                }
            }

            if (!foundCreature || foundIndex < 0)
                throw new System.ArgumentException("CreatureStorage.RemoveCreature: creature " + id + " not found");
            else if (foundCreature == Player)
                throw new System.Exception("CreatureStorage.RemoveCreature: cannot remove player.");

            if (foundCreature == Aim) {
                Aim = null;
                UpdateExtendedMark(foundCreature);
            }

            if (foundCreature == AttackTarget) {
                AttackTarget = null;
                UpdateExtendedMark(foundCreature);
            }

            if (foundCreature == FollowTarget) {
                FollowTarget = null;
                UpdateExtendedMark(foundCreature);
            }

            if (Trappers != null) {
                int index = Trappers.FindIndex((x) => x == foundCreature);
                if (index > 0) {
                    Trappers[index].Trapper = false;
                    Trappers.RemoveAt(index);
                }
            }

            foundCreature.Reset();
            m_Creatures.RemoveAt(foundIndex);
            m_CreatureCount--;
            m_OpponentState = OpponentStates.Rebuild;
        }

        internal void Animate() {
            int ticks = OpenTibiaUnity.TicksMillis;

            foreach (var creature in m_Creatures) {
                if (creature) {
                    creature.AnimateMovement(ticks);
                    creature.AnimateOutfit(ticks);
                }
            }
        }

        internal void ToggleAttackTarget(Creature attack, bool send) {
            if (attack == Player) {
                throw new System.ArgumentException("CreatureStorage.ToggleAttackTarget: Cannot attack player.");
            }

            var creature = AttackTarget;
            if (creature != attack)
                AttackTarget = attack;
            else
                AttackTarget = null;

            if (send) {
                var protocolGame = OpenTibiaUnity.ProtocolGame;
                if (protocolGame)
                    protocolGame.SendAttack(AttackTarget ? AttackTarget.ID : 0);
            }

            UpdateExtendedMark(creature);
            UpdateExtendedMark(AttackTarget);
            
            if (FollowTarget) {
                creature = FollowTarget;
                FollowTarget = null;
                UpdateExtendedMark(creature);
            }
        }

        internal void ClearTargets() {
            var optionStorage = OpenTibiaUnity.OptionStorage;
            if (AttackTarget != null && optionStorage.AutoChaseOff && optionStorage.CombatChaseMode != CombatChaseModes.Off) {
                optionStorage.CombatChaseMode = CombatChaseModes.Off;
                var protocolGame = OpenTibiaUnity.ProtocolGame;
                if (protocolGame != null && protocolGame.IsGameRunning)
                    protocolGame.SendSetTactics();
            }

            if (FollowTarget != null)
                SetFollowTarget(null, true);
        }

        internal void SetAttackTarget(Creature attack, bool send) {
            if (attack == Player)
                throw new System.ArgumentException("CreatureStorage.SetAttackTarget: Cannot follow player.");

            var creature = AttackTarget;
            if (creature != attack) {
                AttackTarget = attack;

                if (send) {
                    var protocolGame = OpenTibiaUnity.ProtocolGame;
                    if (protocolGame)
                        protocolGame.SendAttack(AttackTarget ? AttackTarget.ID : 0);
                }

                UpdateExtendedMark(creature);
                UpdateExtendedMark(AttackTarget);
            }

            if (FollowTarget) {
                creature = FollowTarget;
                FollowTarget = null;
                UpdateExtendedMark(creature);
            }
        }
    }
}
