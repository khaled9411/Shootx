namespace Shootx.Audio
{
    /// <summary>
    /// Logical category identifying the audio bus/group routing and mixing behavior.
    /// </summary>
    public enum SoundCategory
    {
        Master,
        Music,
        SFX,
        Weapons,
        Melee,
        Movement,
        Dash,
        Impacts,
        Pickups,
        Environment,
        UI,
        Announcer
    }

    /// <summary>
    /// Type-safe, stable identifiers for every sound effect in the game.
    /// Allows gameplay code to trigger audio cleanly without relying on brittle strings or hardcoded clips.
    /// </summary>
    public enum SoundId
    {
        None = 0,

        // ─── UI & Menus ──────────────────────────────────────────
        UI_ButtonClick = 100,
        UI_ButtonBack = 101,
        UI_TabSelect = 102,
        UI_PanelOpen = 103,
        UI_PanelClose = 104,
        UI_ActionSuccess = 105,
        UI_ActionFailed = 106,
        UI_LobbyReady = 107,
        UI_LobbyUnready = 108,
        UI_MatchStartButton = 109,
        UI_SliderTick = 110,
        UI_Toggle = 111,

        // ─── Locomotion & Movement ──────────────────────────────
        Movement_Footstep = 200,
        Movement_Footstep_Dirt = 201,
        Movement_Footstep_Metal = 202,

        // ─── Dash Ability ────────────────────────────────────────
        Dash_Whoosh = 300,
        Dash_Impact = 301,

        // ─── Weapons & Shooting ──────────────────────────────────
        Weapon_Revolver_Shoot = 400,
        Weapon_DryFire = 401,
        Weapon_BasicAmmoRefunded = 402,
        Impact_Wall = 410,
        Impact_Ricochet = 411,
        Impact_DestructibleBox = 412,
        Impact_PlayerHit = 413,

        // ─── Melee Combat ────────────────────────────────────────
        Melee_Swing = 500,
        Melee_Hit_Player = 501,
        Melee_Hit_Environment = 502,

        // ─── Destructible Environment ────────────────────────────
        Box_Destroyed = 600,

        // ─── Pickups & Interactivity ─────────────────────────────
        Pickup_Ammo_World = 700,
        Pickup_Ammo_Local = 701,

        // ─── Player Lifecycle ────────────────────────────────────
        Player_Hurt = 800,
        Player_Death = 801,
        Player_Respawn = 802,
        Player_SpawnProtection_Start = 803,
        Player_SpawnProtection_End = 804,

        // ─── Match Events & Announcer ────────────────────────────
        Match_CountdownTick = 900,
        Match_CountdownFinal = 901,
        Match_Go = 902,
        Match_TimerWarning = 903,
        Match_KillConfirmed = 904,
        Match_Victory = 905,
        Match_Defeat = 906,
        Match_Draw = 907,
        Match_Results_Open = 908
    }

    /// <summary>
    /// Identifier for background music tracks.
    /// </summary>
    public enum MusicTrackId
    {
        None = 0,
        MainMenu = 1,
        Lobby = 2,
        Gameplay = 3,
        Results = 4
    }
}
