﻿namespace RoRebuildServer.Data.Monster;

public enum MonsterAiType : byte
{
    AiEmpty,
    AiPassive,
    AiPassiveImmobile,
    AiAggressive,
    AiAggressiveImmobile,
    AiLooter,
    AiAssist,
    AiAssistLooter,
    AiAggressiveLooter,
    AiAngry,
    AiPlant
}

public enum MonsterAiState : byte
{
    StateIdle,
    StateRandomMove,
    StateChase,
    StateAbnormal,
    StateSearch,
    StateAttacking,
    StateAdjust,
    StateDead
}

public enum MonsterInputCheck : byte
{
    InWaitEnd,
    InAttacked,
    InReachedTarget,
    InAttackRange,
    InChangeNormal,
    InTargetSearch,
    InEnemyOutOfSight,
    InEnemyOutOfAttackRange,
    InAttackDelayEnd,
    InNeedAttackingAdjust,
    InDeadTimeoutEnd,
    InAllyInCombat
}

public enum MonsterOutputCheck : byte
{
    OutRandomMoveStart,
    OutWaitStart,
    OutStartChase,
    OutTryAttacking,
    OutStartAttacking,
    OutSearch,
    OutChangeNormal,
    OutPerformAttack,
    OutChangeTargets,
    OutTryRevival,
    OutAttackingAdjust,
    OutWaitForever
}