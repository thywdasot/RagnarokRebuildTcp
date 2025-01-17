﻿using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers;

[ClientPacketHandler(PacketType.RandomTeleport)]
public class PacketRandomTeleport : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
		if (connection.Character == null || connection.Player == null)
            return;

        var player = connection.Player;
        if (player.InActionCooldown())
        {
            ServerLogger.Debug("Player random teleport ignored due to cooldown.");
            return;
        }

        if (player.Character.State == CharacterState.Dead)
            return;

        if (player.IsInNpcInteraction)
            return;

        var ch = connection.Character;
        var map = ch.Map;

        var p = new Position();

        do
        {
            p = new Position(GameRandom.Next(0, map.Width - 1), GameRandom.Next(0, map.Height - 1));
        } while (!map.WalkData.IsCellWalkable(p));

        player.AddActionDelay(1.1f); //add 1s to the player's cooldown times. Should lock out immediate re-use.
        ch.ResetState();
        ch.SpawnImmunity = 5f;
        map.TeleportEntity(ref connection.Entity, ch, p);

        var ce = connection.Entity.Get<CombatEntity>();
        ce.ClearDamageQueue();
	}
}