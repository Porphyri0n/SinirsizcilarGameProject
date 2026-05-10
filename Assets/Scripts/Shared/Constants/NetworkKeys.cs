using System;
using UnityEngine;

// Photon için string anahtarlar — oda/oyuncu custom property'leri ve RPC isimleri.
// Bu sabitler aynen kullanılmalı; elle string yazmak typo = bug demektir.
public static class NetworkKeys
{
    // Room Properties
    public const string ROOM_PHASE = "phase";
    public const string ROOM_WAVE = "wave";
    public const string ROOM_CASTLE_HP = "castleHP";

    // Player Properties
    public const string PLAYER_READY = "ready";
    public const string PLAYER_ALIVE = "alive";
    public const string PLAYER_CARRYING = "carrying";
    public const string PLAYER_WEAPON = "weapon";
    public const string PLAYER_IN_TOWER = "inTower";

    // RPC Names
    public const string RPC_SPAWN_SHIP = "RPC_SpawnShip";
    public const string RPC_DESTROY_SHIP = "RPC_DestroyShip";
    public const string RPC_DEPOSIT_RESOURCE = "RPC_DepositResource";
    public const string RPC_START_CRAFT = "RPC_StartCraft";
    public const string RPC_COMPLETE_CRAFT = "RPC_CompleteCraft";
    public const string RPC_PLAYER_DIED = "RPC_PlayerDied";
    public const string RPC_PLAYER_REVIVED = "RPC_PlayerRevived";
    public const string RPC_RING_BELL = "RPC_RingBell";
    public const string RPC_START_SELA = "RPC_StartSela";
    public const string RPC_USE_POTION = "RPC_UsePotion";
    public const string RPC_TAKE_DAMAGE = "RPC_TakeDamage";
    public const string RPC_CASTLE_DAMAGE = "RPC_CastleDamage";
    public const string RPC_PHASE_CHANGE = "RPC_PhaseChange";
    public const string RPC_WAVE_START = "RPC_WaveStart";
    public const string RPC_WAVE_END = "RPC_WaveEnd";
    public const string RPC_GAME_LOST = "RPC_GameLost";
    public const string RPC_DROP_LOOT = "RPC_DropLoot";
    public const string RPC_CARAVAN_APPROACH = "RPC_CaravanApproach";
    public const string RPC_CARAVAN_ARRIVED = "RPC_CaravanArrived";
    public const string RPC_CARAVAN_ATTACKED = "RPC_CaravanAttacked";
    public const string RPC_CARAVAN_DESTROYED = "RPC_CaravanDestroyed";
    public const string RPC_BANDIT_SPAWN = "RPC_BanditSpawn";
    public const string RPC_BANDIT_KILLED = "RPC_BanditKilled";
    public const string RPC_ENTER_TOWER = "RPC_EnterTower";
    public const string RPC_EXIT_TOWER = "RPC_ExitTower";
    public const string RPC_TOWER_FIRE = "RPC_TowerFire";
    public const string RPC_PLAYER_ATTACK = "RPC_PlayerAttack";
    public const string RPC_PLAYER_BLOCK = "RPC_PlayerBlock";
    public const string RPC_EQUIP_WEAPON = "RPC_EquipWeapon";
    public const string RPC_UPGRADE = "RPC_Upgrade";
    public const string RPC_RESOURCE_RECEIVED = "RPC_ResourceReceived";
}
