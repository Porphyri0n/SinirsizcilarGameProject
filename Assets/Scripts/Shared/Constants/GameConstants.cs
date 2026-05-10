using System;
using UnityEngine;

// Oyun geneli sabitler — tag, layer, hız, süre, network değerleri burada toplanır.
public static class GameConstants
{
    // ── GENEL ────────────────────────────────────────────────────────────
    public const int MAX_PLAYERS = 6;
    public const int BOSS_WAVE_INTERVAL = 5;

    // ── KALE ─────────────────────────────────────────────────────────────
    public const float CASTLE_MAX_HP = 1000f;

    // ── EL ARABASI ───────────────────────────────────────────────────────
    public const int WHEELBARROW_BASE_CAPACITY = 3;
    public const float WHEELBARROW_BASE_SPEED = 0.7f;

    // ── HAREKET ──────────────────────────────────────────────────────────
    public const float PLAYER_BASE_SPEED = 7f;
    public const float PLAYER_SPRINT_SPEED = 12f;
    public const float PLAYER_JUMP_FORCE = 8f;
    public const float CARRY_SPEED_MULTIPLIER = 0.6f;

    // ── COMBAT ───────────────────────────────────────────────────────────
    public const float SWORD_BASE_DAMAGE = 10f;
    public const float SHIELD_BASE_BLOCK = 0.3f;
    public const float ATTACK_COOLDOWN = 0.8f;

    // ── İKSİR ────────────────────────────────────────────────────────────
    public const float STRENGTH_POTION_DURATION = 30f;
    public const float HEARING_POTION_DURATION = 45f;
    public const float STRENGTH_MULTIPLIER = 1.5f;
    public const float HEARING_RANGE_MULTIPLIER = 2.0f;

    // ── PROXIMITY CHAT ───────────────────────────────────────────────────
    public const float VOICE_BASE_RANGE = 20f;
    public const float VOICE_FALLOFF_START = 10f;
    public const float TOWER_VOICE_RANGE = 100f;

    // ── INTERACT ─────────────────────────────────────────────────────────
    public const float INTERACT_RANGE = 3f;
    public const KeyCode INTERACT_KEY = KeyCode.E;

    // ── WAVE SCALING ─────────────────────────────────────────────────────
    public const float WAVE_DIFFICULTY_MULTIPLIER = 1.15f;
    public const float PREP_BASE_DURATION = 90f;
    public const float PREP_MIN_DURATION = 45f;

    // ── KERVAN (Ticari At Arabası) ───────────────────────────────────────
    public const int CARAVAN_FIRST_WAVE = 3;            // İlk kervan wave 3'ten sonra
    public const int CARAVAN_INTERVAL = 2;              // Her 2 wave'de bir kervan
    public const float BANDIT_BASE_CHANCE = 0.3f;       // %30 haydut şansı
    public const float BANDIT_CHANCE_INCREASE = 0.05f;  // Her wave %5 artış

    // ── NETWORK ──────────────────────────────────────────────────────────
    public const int MAX_PLAYERS_PER_ROOM = 6;
    public const float NETWORK_SYNC_RATE = 0.1f;

    // ── TAGS ─────────────────────────────────────────────────────────────
    public const string TAG_PLAYER = "Player";
    public const string TAG_ENEMY = "Enemy";
    public const string TAG_DEFENSE = "Defense";
    public const string TAG_CASTLE = "Castle";
    public const string TAG_INTERACTABLE = "Interactable";
    public const string TAG_LOOT = "Loot";
    public const string TAG_CARAVAN = "Caravan";
    public const string TAG_BANDIT = "Bandit";
    public const string TAG_TRADE_ROAD = "TradeRoad";

    // ── LAYERS ───────────────────────────────────────────────────────────
    public const string LAYER_GROUND = "Ground";
    public const string LAYER_WATER = "Water";
    public const string LAYER_WALL_TOP = "WallTop";     // Sur üstü yürünebilir alan
}
