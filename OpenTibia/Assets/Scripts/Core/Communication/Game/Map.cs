﻿namespace OpenTibiaUnity.Core.Communication.Game
{
    internal partial class ProtocolGame : Internal.Protocol
    {
        private UnityEngine.Vector3Int m_LastSnapback = UnityEngine.Vector3Int.zero;
        private int m_SnapbackCount = 0;

        private void ParseFullMap(Internal.ByteArray message) {
            UnityEngine.Vector3Int position = message.ReadPosition();

            Player.StopAutowalk(true);
            CreatureStorage.MarkAllOpponentsVisible(false);
            MiniMapStorage.Position = position;
            WorldMapStorage.ResetMap();
            WorldMapStorage.InvalidateOnscreenMessages();
            WorldMapStorage.Position = position;
            
            ReadArea(message, 0, 0, Constants.MapSizeX - 1, Constants.MapSizeY - 1);
            WorldMapStorage.Valid = true;
        }

        private void ParseMapTopRow(Internal.ByteArray message) {
            UnityEngine.Vector3Int position = WorldMapStorage.Position;
            position.y--;

            WorldMapStorage.Position = position;
            MiniMapStorage.Position = position;
            WorldMapStorage.ScrollMap(0, 1);
            WorldMapStorage.InvalidateOnscreenMessages();
            ReadArea(message, 0, 0, Constants.MapSizeX - 1, 0);
        }

        private void ParseMapRightRow(Internal.ByteArray message) {
            UnityEngine.Vector3Int position = WorldMapStorage.Position;
            position.x++;

            WorldMapStorage.Position = position;
            MiniMapStorage.Position = position;
            WorldMapStorage.ScrollMap(-1, 0);
            WorldMapStorage.InvalidateOnscreenMessages();
            ReadArea(message, Constants.MapSizeX - 1, 0, Constants.MapSizeX - 1, Constants.MapSizeY - 1);
        }

        private void ParseMapBottomRow(Internal.ByteArray message) {
            UnityEngine.Vector3Int position = WorldMapStorage.Position;
            position.y++;

            WorldMapStorage.Position = position;
            MiniMapStorage.Position = position;
            WorldMapStorage.ScrollMap(0, -1);
            WorldMapStorage.InvalidateOnscreenMessages();
            ReadArea(message, 0, Constants.MapSizeY - 1, Constants.MapSizeX - 1, Constants.MapSizeY - 1);
        }

        private void ParseMapLeftRow(Internal.ByteArray message) {
            UnityEngine.Vector3Int position = WorldMapStorage.Position;
            position.x--;

            WorldMapStorage.Position = position;
            MiniMapStorage.Position = position;
            WorldMapStorage.ScrollMap(1, 0);
            WorldMapStorage.InvalidateOnscreenMessages();
            ReadArea(message, 0, 0, 0, Constants.MapSizeY - 1);
        }

        private void ParseFieldData(Internal.ByteArray message) {
            UnityEngine.Vector3Int absolutePosition = message.ReadPosition();
            if (!WorldMapStorage.IsVisible(absolutePosition, true))
                throw new System.Exception("ProtocolGame.ParseFieldData: Co-ordinate " + absolutePosition + " is out of range.");

            var mapPosition = WorldMapStorage.ToMap(absolutePosition);
            WorldMapStorage.ResetField(mapPosition, true, false);
            ReadField(message, mapPosition.x, mapPosition.y, mapPosition.z);

            if (absolutePosition.z == MiniMapStorage.PositionZ) {
                WorldMapStorage.UpdateMiniMap(mapPosition);
                uint color = WorldMapStorage.GetMiniMapColour(mapPosition);
                int cost = WorldMapStorage.GetMiniMapCost(mapPosition);
                MiniMapStorage.UpdateField(absolutePosition, color, cost, false);
            }
        }

        private void ParseCreateOnMap(Internal.ByteArray message) {
            var absolutePosition = message.ReadPosition();
            if (!WorldMapStorage.IsVisible(absolutePosition, true))
                throw new System.Exception("ProtocolGame.ParseCreateOnMap: Co-ordinate " + absolutePosition + " is out of range.");

            var mapPosition = WorldMapStorage.ToMap(absolutePosition);
            int stackPos = 255;
            if (OpenTibiaUnity.GameManager.ClientVersion >= 841)
                stackPos = message.ReadUnsignedByte();
            
            int typeOrId = message.ReadUnsignedShort();
            
            Appearances.ObjectInstance @object;
            if (typeOrId == Appearances.AppearanceInstance.Creature || typeOrId == Appearances.AppearanceInstance.OutdatedCreature || typeOrId == Appearances.AppearanceInstance.UnknownCreature) {
                var creature = ReadCreatureInstance(message, typeOrId, absolutePosition);
                if (creature.ID == Player.ID)
                    Player.StopAutowalk(true);
                
                @object = AppearanceStorage.CreateObjectInstance(Appearances.AppearanceInstance.Creature, creature.ID);
            } else {
                @object = ReadObjectInstance(message, typeOrId);
            }

            if (stackPos == 255) {
                WorldMapStorage.PutObject(mapPosition, @object);
            } else {
                if (stackPos > Constants.MapSizeW)
                    throw new System.Exception("ProtocolGame.ParseCreateOnMap: Invalid stack position (" + stackPos + ").");

                WorldMapStorage.InsertObject(mapPosition, stackPos, @object);
            }

            if (absolutePosition.z == MiniMapStorage.PositionZ) {
                WorldMapStorage.UpdateMiniMap(mapPosition);
                uint color = WorldMapStorage.GetMiniMapColour(mapPosition);
                int cost = WorldMapStorage.GetMiniMapCost(mapPosition);
                MiniMapStorage.UpdateField(absolutePosition, color, cost, false);
            }
        }

        private void ParseChangeOnMap(Internal.ByteArray message) {
            int x = message.ReadUnsignedShort();
            Appearances.ObjectInstance objectInstance;
            Creatures.Creature creature = null;

            UnityEngine.Vector3Int absolutePosition;
            UnityEngine.Vector3Int mapPosition;

            if (x != 65535) {
                absolutePosition = message.ReadPosition(x);
                if (!WorldMapStorage.IsVisible(absolutePosition, true))
                    throw new System.Exception("ProtocolGame.ParseChangeOnMap: Co-ordinate " + absolutePosition + " is out of range.");

               mapPosition = WorldMapStorage.ToMap(absolutePosition);
                int stackPos = message.ReadUnsignedByte();
                if (!(objectInstance = WorldMapStorage.GetObject(mapPosition, stackPos)))
                    throw new System.Exception("ProtocolGame.ParseChangeOnMap: Object not found.");

                if (objectInstance.IsCreature && !(creature = CreatureStorage.GetCreature(objectInstance.Data)))
                    throw new System.Exception("ProtocolGame.ParseChangeOnMap: Creature not found: " + objectInstance.Data);

                if (!!creature)
                    CreatureStorage.MarkOpponentVisible(creature, false);

                int typeOrId = message.ReadUnsignedShort();
                if (typeOrId == Appearances.AppearanceInstance.UnknownCreature
                        || typeOrId == Appearances.AppearanceInstance.OutdatedCreature
                        || typeOrId == Appearances.AppearanceInstance.Creature) {
                    creature = ReadCreatureInstance(message, typeOrId, absolutePosition);
                    objectInstance = AppearanceStorage.CreateObjectInstance(Appearances.AppearanceInstance.Creature, creature.ID);
                } else {
                    objectInstance = ReadObjectInstance(message, typeOrId);
                }

                WorldMapStorage.ChangeObject(mapPosition, stackPos, objectInstance);
            } else {
                uint creatureID = message.ReadUnsignedInt();

                if (!(creature = CreatureStorage.GetCreature(creatureID)))
                    throw new System.Exception("ProtocolGame.ParseChangeOnMap: Creature " + creatureID + " not found");

                absolutePosition = creature.Position;
                if (!WorldMapStorage.IsVisible(absolutePosition, true))
                    throw new System.Exception("ProtocolGame.ParseChangeOnMap: Co-ordinate " + absolutePosition + " is out of range.");

                mapPosition = WorldMapStorage.ToMap(absolutePosition);
                CreatureStorage.MarkOpponentVisible(creature, false);

                int otherType = message.ReadUnsignedShort();
                if (otherType == Appearances.AppearanceInstance.Creature || otherType == Appearances.AppearanceInstance.OutdatedCreature
                    || otherType == Appearances.AppearanceInstance.UnknownCreature) {
                    creature = ReadCreatureInstance(message, otherType);
                } else {
                    throw new System.Exception("ProtocolGame.ParseChangeOnMap: Received object of type " + otherType + " when a creature was expected.");
                }
            }

            if (absolutePosition.z == MiniMapStorage.PositionZ) {
                WorldMapStorage.UpdateMiniMap(mapPosition);
                uint color = WorldMapStorage.GetMiniMapColour(mapPosition);
                int cost = WorldMapStorage.GetMiniMapCost(mapPosition);
                MiniMapStorage.UpdateField(absolutePosition, color, cost, false);
            }
        }

        private void ParseDeleteOnMap(Internal.ByteArray message) {
            int x = message.ReadUnsignedShort();

            Appearances.ObjectInstance objectInstance;
            Creatures.Creature creature = null;

            UnityEngine.Vector3Int absolutePosition;
            UnityEngine.Vector3Int mapPosition;

            if (x != 65535) {
                absolutePosition = message.ReadPosition(x);

                if (!WorldMapStorage.IsVisible(absolutePosition, true)) {
                    throw new System.Exception($"ProtocolGame.ParseDeleteOnMap: Co-oridnate ({absolutePosition.x}, {absolutePosition.y}, {absolutePosition.z}) is out of range.");
                }

                mapPosition = WorldMapStorage.ToMap(absolutePosition);

                int stackPos = message.ReadUnsignedByte();
                if (!(objectInstance = WorldMapStorage.GetObject(mapPosition, stackPos))) {
                    throw new System.Exception($"ProtocolGame.ParseDeleteOnMap: Object not found.");
                }

                if (objectInstance.IsCreature && (creature = CreatureStorage.GetCreature(objectInstance.Data)) == null) {
                    throw new System.Exception($"ProtocolGame.ParseDeleteOnMap: Creature not found.");
                }

                WorldMapStorage.DeleteObject(mapPosition, stackPos);
            } else {
                uint creatureID = message.ReadUnsignedInt();
                if ((creature = CreatureStorage.GetCreature(creatureID)) == null) {
                    throw new System.Exception($"ProtocolGame.ParseDeleteOnMap: Object not found.");
                }

                absolutePosition = creature.Position;
                if (!WorldMapStorage.IsVisible(absolutePosition, true)) {
                    throw new System.Exception($"ProtocolGame.ParseDeleteOnMap: Co-oridnate ({absolutePosition.x}, {absolutePosition.y}, {absolutePosition.z}) is out of range.");
                }

                mapPosition = WorldMapStorage.ToMap(absolutePosition);
            }

            if (!!creature) {
                CreatureStorage.MarkOpponentVisible(creature, false);
            }

            if (absolutePosition.z == MiniMapStorage.Position.z) {
                WorldMapStorage.UpdateMiniMap(mapPosition);
                uint color = WorldMapStorage.GetMiniMapColour(mapPosition);
                int cost = WorldMapStorage.GetMiniMapCost(mapPosition);
                MiniMapStorage.UpdateField(absolutePosition, color, cost, false);
            }
        }

        private void ParseCreatureMove(Internal.ByteArray message) {
            int x = message.ReadUnsignedShort();

            UnityEngine.Vector3Int oldAbsolutePosition;
            UnityEngine.Vector3Int oldMapPosition;
            int stackPos = -1;
            Appearances.ObjectInstance @object;
            Creatures.Creature creature;
            
            if (x != 65535) {
                oldAbsolutePosition = message.ReadPosition(x);
                if (!WorldMapStorage.IsVisible(oldAbsolutePosition, true))
                    throw new System.Exception("ProtocolGame.ParseCreatureMove: Start Co-ordinate " + oldAbsolutePosition + " is out of range.");

                oldMapPosition = WorldMapStorage.ToMap(oldAbsolutePosition);
                stackPos = message.ReadUnsignedByte();
                @object = WorldMapStorage.GetObject(oldMapPosition, stackPos);
                if (!@object || !@object.IsCreature || !(creature = CreatureStorage.GetCreature(@object.Data)))
                    throw new System.Exception("ProtocolGame.ParseCreatureMove: No creature at position " + oldAbsolutePosition);
            } else {
                uint creatureID = message.ReadUnsignedInt();
                @object = AppearanceStorage.CreateObjectInstance(Appearances.AppearanceInstance.Creature, creatureID);
                if (!(creature = CreatureStorage.GetCreature(creatureID)))
                    throw new System.Exception("ProtocolGame.ParseCreatureMove: Creature " + creatureID + " not found");

                oldAbsolutePosition = creature.Position;
                if (!WorldMapStorage.IsVisible(oldAbsolutePosition, true))
                    throw new System.Exception("ProtocolGame.ParseCreatureMove: Start Co-ordinate " + oldAbsolutePosition + " is out of range.");

                oldMapPosition = WorldMapStorage.ToMap(oldAbsolutePosition);
            }

           var newAbsolutePosition = message.ReadPosition();
            if (!WorldMapStorage.IsVisible(newAbsolutePosition, true))
                throw new System.Exception("ProtocolGame.ParseCreatureMove: Target Co-ordinate " + oldAbsolutePosition + " is out of range.");

            var newMapPosition = WorldMapStorage.ToMap(newAbsolutePosition);
            var delta = newMapPosition - oldMapPosition;

            // if the movement is not actually a move (usually he is teleported)
            bool pushMovement = delta.z != 0 || System.Math.Abs(delta.x) > 1 || System.Math.Abs(delta.y) > 1;
            Appearances.ObjectInstance otherObj = null;
            if (!pushMovement && (!(otherObj = WorldMapStorage.GetObject(newMapPosition, 0)) || !otherObj.Type || !otherObj.Type.IsGround))
                throw new System.Exception("ProtocolGame.ParseCreatureMove: Target field " + newAbsolutePosition + " has no BANK.");

            if (x != 65535)
                WorldMapStorage.DeleteObject(oldMapPosition, stackPos);

            WorldMapStorage.PutObject(newMapPosition, @object);
            creature.Position = newAbsolutePosition;
            
            if (pushMovement) {
                if (creature.ID == Player.ID)
                    Player.StopAutowalk(true);

                if (delta.x > 0)
                    creature.Direction = Direction.East;
                else if (delta.x < 0)
                    creature.Direction = Direction.West;
                else if (delta.y < 0)
                    creature.Direction = Direction.North;
                else if (delta.y > 0)
                    creature.Direction = Direction.South;
                
                if (creature.ID != Player.ID)
                    creature.StopMovementAnimation();
            } else {
                creature.StartMovementAnimation(delta.x, delta.y, (int)otherObj.Type.GroundSpeed);
            }

            CreatureStorage.MarkOpponentVisible(creature, true);
            CreatureStorage.InvalidateOpponents();

            if (oldAbsolutePosition.z == MiniMapStorage.PositionZ) {
                WorldMapStorage.UpdateMiniMap(oldMapPosition);
                uint color = WorldMapStorage.GetMiniMapColour(oldMapPosition);
                int cost = WorldMapStorage.GetMiniMapCost(oldMapPosition);
                MiniMapStorage.UpdateField(oldAbsolutePosition, color, cost, false);
            }

            if (newAbsolutePosition.z == MiniMapStorage.PositionZ) {
                WorldMapStorage.UpdateMiniMap(newMapPosition);
                uint color = WorldMapStorage.GetMiniMapColour(newMapPosition);
                int cost = WorldMapStorage.GetMiniMapCost(newMapPosition);
                MiniMapStorage.UpdateField(newAbsolutePosition, color, cost, false);
            }
        }

        private void ParseCancelWalk(Internal.ByteArray message) {
            int direction = message.ReadUnsignedByte();

            var absolutePosition = Player.Position;
            if (absolutePosition == m_LastSnapback)
                m_SnapbackCount++;
            else
                m_SnapbackCount = 0;

            m_LastSnapback.Set(absolutePosition.x, absolutePosition.y, absolutePosition.z);
            if (m_SnapbackCount >= 16) {
                Player.StopAutowalk(true);
                CreatureStorage.SetAttackTarget(null, false);
                SendCancel();
                m_SnapbackCount = 0;
            }

            Player.AbortAutowalk((Direction)direction);
        }

        private void ParseWait(Internal.ByteArray message) {
            ushort ticks = message.ReadUnsignedShort();
            Player.EarliestMoveTime += ticks;
        }

        private void ParseMapTopFloor(Internal.ByteArray message) {
            UnityEngine.Vector3Int position = WorldMapStorage.Position;
            position.x++; position.y++; position.z--;

            WorldMapStorage.Position = position;
            MiniMapStorage.Position = position;

            if (position.z > Constants.GroundLayer) {
                WorldMapStorage.ScrollMap(0, 0, -1);
                ReadFloor(message, 2 * Constants.UndergroundLayer, 0);
            } else if (position.z == Constants.GroundLayer) {
                WorldMapStorage.ScrollMap(0, 0, -(Constants.UndergroundLayer + 1));
                int skip = 0;
                for (int zposition = Constants.UndergroundLayer; zposition <= Constants.GroundLayer; zposition++)
                    skip = ReadFloor(message, zposition, skip);
            }

            Player.StopAutowalk(true);
            WorldMapStorage.InvalidateOnscreenMessages();

            UnityEngine.Vector3Int tmpPosition = WorldMapStorage.ToMap(position);
            
            for (int x = 0; x < Constants.MapSizeX; x++) {
                for (int y = 0; x < Constants.MapSizeY; y++) {
                    tmpPosition.x = x;
                    tmpPosition.y = y;

                    UnityEngine.Vector3Int absolutePosition = WorldMapStorage.ToAbsolute(tmpPosition);
                    WorldMapStorage.UpdateMiniMap(tmpPosition);
                    uint color = WorldMapStorage.GetMiniMapColour(tmpPosition);
                    int cost = WorldMapStorage.GetMiniMapCost(tmpPosition);
                    MiniMapStorage.UpdateField(absolutePosition, color, cost, false);
                }
            }
        }

        private void ParseMapBottomFloor(Internal.ByteArray message) {
            UnityEngine.Vector3Int position = WorldMapStorage.Position;
            position.x--; position.y--; position.z++;

            WorldMapStorage.Position = position;
            MiniMapStorage.Position = position;

            if (position.z > Constants.GroundLayer + 1) {
                WorldMapStorage.ScrollMap(0, 0, 1);
                if (position.z <= Constants.MapMaxZ - Constants.UndergroundLayer) {
                    ReadFloor(message, 2 * Constants.UndergroundLayer, 0);
                }
            } else if (position.z == Constants.GroundLayer + 1) {
                WorldMapStorage.ScrollMap(0, 0, Constants.UndergroundLayer + 1);
                int skip = 0;
                for (int zposition = Constants.UndergroundLayer; zposition >= 0; zposition--)
                    skip = ReadFloor(message, zposition, skip);
            }

            Player.StopAutowalk(true);
            WorldMapStorage.InvalidateOnscreenMessages();

            UnityEngine.Vector3Int tmpPosition = WorldMapStorage.ToMap(position);

            for (int x = 0; x < Constants.MapSizeX; x++) {
                for (int y = 0; x < Constants.MapSizeY; y++) {
                    tmpPosition.x = x;
                    tmpPosition.y = y;

                    UnityEngine.Vector3Int absolutePosition = WorldMapStorage.ToAbsolute(tmpPosition);
                    WorldMapStorage.UpdateMiniMap(tmpPosition);
                    uint color = WorldMapStorage.GetMiniMapColour(tmpPosition);
                    int cost = WorldMapStorage.GetMiniMapCost(tmpPosition);
                    MiniMapStorage.UpdateField(absolutePosition, color, cost, false);
                }
            }
        }

        private void ParseAutomapFlag(Internal.ByteArray message) {
            var absolutePosition = message.ReadPosition();
            int icon = message.ReadUnsignedByte();
            var description = message.ReadString();

            bool remove = false;
            if (OpenTibiaUnity.GameManager.GetFeature(GameFeature.GameMinimapRemove))
                remove = message.ReadBoolean();
        }
    }
}
