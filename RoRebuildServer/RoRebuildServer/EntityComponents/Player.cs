﻿using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntityComponents;

[EntityComponent(EntityType.Player)]
public class Player : IEntityAutoReset
{
    public Entity Entity;
    public WorldObject Character = null!;
    public CombatEntity CombatEntity = null!;

    public NetworkConnection Connection;

    [EntityIgnoreNullCheck]
    public NpcInteractionState NpcInteractionState = new();

    [EntityIgnoreNullCheck]
    public int[] CharData = new int[(int)PlayerStat.PlayerStatsMax];

    public Guid Id { get; set; }
    public string Name { get; set; }
    public float CurrentCooldown;
    public HeadFacing HeadFacing;
    //public PlayerData Data { get; set; }
    public bool IsAdmin { get; set; }
    
    public int HeadId => GetData(PlayerStat.Head);
    public bool IsMale => GetData(PlayerStat.Gender) == 0;

    public bool IsInNpcInteraction;
        
    public Entity Target { get; set; }
        
    public bool QueueAttack { get; set; }
    private float regenTickTime { get; set; }

    public int GetData(PlayerStat type) => CharData[(int)type];
    public void SetData(PlayerStat type, int val) => CharData[(int)type] = val;
    public int GetStat(CharacterStat type) => CombatEntity.GetStat(type);
    public float GetTiming(TimingStat type) => CombatEntity.GetTiming(type);
    public void SetStat(CharacterStat type, int val) => CombatEntity.SetStat(type, val);
    public void SetStat(CharacterStat type, float val) => CombatEntity.SetStat(type, (int)val);
    public void SetTiming(TimingStat type, float val) => CombatEntity.SetTiming(type, val);
    
    public void Reset()
    {
        Entity = Entity.Null;
        Target = Entity.Null;
        Character = null!;
        CombatEntity = null!;
        Connection = null!;
        CurrentCooldown = 0f;
        HeadFacing = HeadFacing.Center;
        QueueAttack = false;
        Id = Guid.Empty;
        Name = "Player";
        //Data = new PlayerData(); //fix this...
        regenTickTime = 0f;
        NpcInteractionState.Reset();
        IsAdmin = false;
        for(var i = 0; i < CharData.Length; i++)
            CharData[i] = 0;
    }

    public void Init()
    {
        if (GetData(PlayerStat.Status) == 0)
        {
            SetData(PlayerStat.Level, 1);
            SetData(PlayerStat.Head, GameRandom.Next(0, 31));
            SetData(PlayerStat.Gender, GameRandom.Next(0, 1));
            SetData(PlayerStat.Status, 1);
        }

        UpdateStats();

        SetStat(CharacterStat.Level, GetData(PlayerStat.Level));
        IsAdmin = true; //for now
    }
    
    public void UpdateStats()
    {
        var level = GetData(PlayerStat.Level);
        var aMotionTime = 1.1f - level * 0.006f;
        var spriteAttackTiming = 0.6f;

        if (spriteAttackTiming > aMotionTime)
            spriteAttackTiming = aMotionTime;

        SetTiming(TimingStat.AttackMotionTime, aMotionTime);
        SetTiming(TimingStat.SpriteAttackTiming, spriteAttackTiming);
        SetTiming(TimingStat.AttackDelayTime, 0); //fixme!
        SetTiming(TimingStat.HitDelayTime, 0.5f);
        SetTiming(TimingStat.MoveSpeed, 0.15f);
        SetStat(CharacterStat.Range, 2);

        var atk = (level / 2f) * (level / 2f) + level * (level / 10) + 12 + level;
        atk *= 1.2f;
        var atk1 = (int)(atk * 0.90f - 1);
        var atk2 = (int)(atk * 1.10f + 1);

        var multiplier = 0.1f + level / 10f;
        if (multiplier > 1f)
            multiplier = 1f;

        SetStat(CharacterStat.Attack, atk1);
        SetStat(CharacterStat.Attack2, atk2);
        SetStat(CharacterStat.Def, level * 0.7f);
        SetStat(CharacterStat.Vit, 3 + level * 1.5f);
        SetStat(CharacterStat.MaxHp, 50 + 100 * level);

        var newMaxHp = (level * level * level) / 20 + 80 * level;
        var updatedMaxHp = (int)(newMaxHp * multiplier) + 70;

        SetStat(CharacterStat.MaxHp, updatedMaxHp);
        if(GetStat(CharacterStat.Hp) <= 0)
            SetStat(CharacterStat.Hp, updatedMaxHp);

        var moveSpeed = 0.15f - (0.001f * level / 3f);
        SetTiming(TimingStat.MoveSpeed, moveSpeed);
        Character.MoveSpeed = moveSpeed;
    }

    public void LevelUp()
    {
        var level = GetData(PlayerStat.Level);
        var aMotionTime = 1.1f - level * 0.006f;
        var spriteAttackTiming = 0.6f;

        level++;

        SetData(PlayerStat.Level, level);
        SetStat(CharacterStat.Level, level);

        UpdateStats();

        CombatEntity.FullRecovery(true, true);
    }

    public void SaveCharacterToData()
    {
        SetData(PlayerStat.Hp, GetStat(CharacterStat.Hp));
        SetData(PlayerStat.Mp, GetStat(CharacterStat.Mp));
    }

    public void ApplyDataToCharacter()
    {
        SetStat(CharacterStat.Hp, GetData(PlayerStat.Hp));
        SetStat(CharacterStat.Mp, GetData(PlayerStat.Mp));
    }
    
    public void UpdateSit(bool isSitting)
    {
        if (!isSitting)
        {
            regenTickTime += 4f;
            if (regenTickTime > Time.ElapsedTimeFloat + 8f)
                regenTickTime = Time.ElapsedTimeFloat + 8f;
        }
        else
        {
            if (regenTickTime > Time.ElapsedTimeFloat + 4f)
                regenTickTime = Time.ElapsedTimeFloat + 4;
        }
    }
    public void RegenTick()
    {
        if (!Character.IsActive || Character.State == CharacterState.Dead)
            return;

        var hp = GetStat(CharacterStat.Hp);
        var maxHp = GetStat(CharacterStat.MaxHp);

        if (hp < maxHp)
        {
            var regen = maxHp / 10;
            if (Character.State == CharacterState.Sitting)
                regen *= 2;
            if (regen + hp > maxHp)
                regen = maxHp - hp;


            SetStat(CharacterStat.Hp, hp + regen);

            CommandBuilder.SendHealSingle(this, regen, HealType.None);
        }
    }

    public void Die()
    {
        if (Character.State == CharacterState.Dead)
            return; //we're already dead!

        ClearTarget();
        Character.StopMovingImmediately();
        Character.State = CharacterState.Dead;

        Character.Map.GatherPlayersForMultiCast(ref Entity, Character);
        CommandBuilder.SendPlayerDeath(Character);
        CommandBuilder.ClearRecipients();
    }

    private bool ValidateTarget()
    {
        if (Target.IsNull() || !Target.IsAlive())
        {
            ClearTarget();
            return false;
        }

        var ce = Target.Get<CombatEntity>();
        if (ce == null || !ce.IsValidTarget(CombatEntity))
            return false;

        return true;
    }

    public void ClearTarget()
    {
        QueueAttack = false;

        if (!Target.IsNull())
            CommandBuilder.SendChangeTarget(this, null);

        Target = Entity.Null;
    }

    public void ChangeTarget(WorldObject target)
    {
        if (target == null || Target == target.Entity)
            return;

        CommandBuilder.SendChangeTarget(this, target);

        Target = target.Entity;
    }


    public void SaveSpawnPoint(string mapName, int x, int y, int size = 1)
    {

    }


    public void PerformQueuedAttack()
    {
        //QueueAttack = false;
        if (!ValidateTarget())
        {
            QueueAttack = false;
            return;
        }

        var targetCharacter = Target.Get<WorldObject>();
        if (!targetCharacter.IsActive)
        {
            QueueAttack = false;
            return;
        }

        if (targetCharacter.Map != Character.Map)
        {
            QueueAttack = false;
            return;
        }

        if (Character.Position.SquareDistance(targetCharacter.Position) > GetStat(CharacterStat.Range))
        {
            TargetForAttack(targetCharacter);
            return;
        }

        PerformAttack(targetCharacter);
    }
    public void PerformAttack(WorldObject targetCharacter)
    {
        if (targetCharacter.Type == CharacterType.NPC)
        {
            ChangeTarget(null);

            return;
        }

        var targetEntity = targetCharacter.Entity.Get<CombatEntity>();
        if (!targetEntity.IsValidTarget(CombatEntity))
        {
            ClearTarget();
            return;
        }

        Character.StopMovingImmediately();

        if (Character.AttackCooldown > Time.ElapsedTimeFloat)
        {
            QueueAttack = true;
            if (Target != targetCharacter.Entity)
                ChangeTarget(targetCharacter);

            return;
        }

        Character.SpawnImmunity = -1;

        CombatEntity.PerformMeleeAttack(targetEntity);
        Character.AddMoveDelay(GetTiming(TimingStat.AttackDelayTime));

        QueueAttack = true;

        Character.AttackCooldown = Time.ElapsedTimeFloat + GetTiming(TimingStat.AttackMotionTime);
    }


    public void TargetForAttack(WorldObject enemy)
    {
        if (Character.Position.SquareDistance(enemy.Position) <= GetStat(CharacterStat.Range))
        {
            ChangeTarget(enemy);
            PerformAttack(enemy);
            return;
        }

        if (!Character.TryMove(ref Entity, enemy.Position, 0))
            return;

        ChangeTarget(enemy);
    }

    public void PerformSkill()
    {
        var pool = EntityListPool.Get();
        Character.Map.GatherEnemiesInRange(Character, 7, pool, true);

        if (Character.AttackCooldown > Time.ElapsedTimeFloat)
            return;

        if (pool.Count == 0)
        {
            EntityListPool.Return(pool);
            return;
        }

        Character.StopMovingImmediately();
        ClearTarget();

        for (var i = 0; i < pool.Count; i++)
        {
            var e = pool[i];
            if (e.IsNull() || !e.IsAlive())
                continue;
            var target = e.Get<CombatEntity>();
            if (target == CombatEntity || target.Character.Type == CharacterType.Player)
                continue;

            CombatEntity.PerformMeleeAttack(target);
            Character.AddMoveDelay(GetTiming(TimingStat.AttackDelayTime));
        }

        Character.AttackCooldown = Time.ElapsedTimeFloat + GetTiming(TimingStat.AttackMotionTime);
    }

    public bool WarpPlayer(string mapName, int x, int y, int width, int height, bool failIfNotWalkable)
    {
        if (!World.Instance.TryGetWorldMapByName(mapName, out var map))
            return false;

        AddActionDelay(2f); //block character input for 1+ seconds.
        Character.ResetState();
        Character.SpawnImmunity = 5f;

        CombatEntity.ClearDamageQueue();

        var p = new Position(x, y);

        if (Character.Map != null && (width > 1 || height > 1))
        {
            var area = Area.CreateAroundPoint(x, y, width, height);
            p = Character.Map.GetRandomWalkablePositionInArea(area);
            if (p == Position.Invalid)
            {
                ServerLogger.LogWarning($"Could not warp player to map {mapName} area {area} is blocked.");
                p = new Position(x, y);
            }
        }
        
        if (Character.Map?.Name == mapName)
            Character.Map.TeleportEntity(ref Entity, Character, p, false, CharacterRemovalReason.OutOfSight);
        else
            World.Instance.MovePlayerMap(ref Entity, Character, map, p);

        return true;
    }


    public void UpdatePosition()
    {
        //var connector = DataManager.GetConnector(Character.Map.Name, nextPos);

        //if (connector != null)
        //{
        //    Character.State = CharacterState.Idle;

        //    if (connector.Map == connector.Target)
        //        Character.Map.MoveEntity(ref Entity, Character, connector.DstArea.RandomInArea());
        //    else
        //        Character.Map.World.MovePlayerMap(ref Entity, Character, connector.Target, connector.DstArea.RandomInArea());

        //    CombatEntity.ClearDamageQueue();

        //    return;
        //}

        if (!ValidateTarget())
            return;

        var targetCharacter = Target.Get<WorldObject>();

        if (Character.State == CharacterState.Moving)
        {
            if (Character.Position.SquareDistance(targetCharacter.Position) <= GetStat(CharacterStat.Range))
                PerformAttack(targetCharacter);
        }

        if (Character.State == CharacterState.Idle)
        {
            TargetForAttack(targetCharacter);
        }
    }


    public bool InActionCooldown() => CurrentCooldown > 1f;
    public void AddActionDelay(CooldownActionType type) => CurrentCooldown += ActionDelay.CooldownTime(type);
    public void AddActionDelay(float time) => CurrentCooldown += CurrentCooldown;

    public void Update()
    {
        if (QueueAttack)
        {
            if (Character.AttackCooldown < Time.ElapsedTimeFloat)
                PerformQueuedAttack();
        }

        if (regenTickTime < Time.ElapsedTimeFloat)
        {
            RegenTick();
            if (Character.State == CharacterState.Sitting)
                regenTickTime = Time.ElapsedTimeFloat + 4f;
            else
                regenTickTime = Time.ElapsedTimeFloat + 8f;
        }

        CurrentCooldown -= Time.DeltaTimeFloat;

        if (CurrentCooldown < 0)
            CurrentCooldown = 0;


    }
}