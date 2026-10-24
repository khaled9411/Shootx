#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Shootx.Audio.Editor
{
    /// <summary>
    /// Editor utility to initialize, configure, and validate the Shootx Audio System.
    /// Sets up the AudioLibrary, populates sound definitions with sensible defaults,
    /// maps existing audio files from the project, and audits missing assets.
    /// </summary>
    public static class AudioSystemSetupUtility
    {
        private const string AudioRootPath = "Assets/_Multiplayer/Audio";
        private const string LibrariesPath = "Assets/_Multiplayer/Audio/Libraries";
        private const string MixersPath = "Assets/_Multiplayer/Audio/Mixers";
        private const string LibraryAssetPath = "Assets/_Multiplayer/Audio/Libraries/GameAudioLibrary.asset";

        [InitializeOnLoadMethod]
        public static void AutoRunOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                SetupAudioSystem();
            };
        }

        [MenuItem("Tools/Shootx/Audio/Setup Complete Audio System", false, 10)]
        public static void SetupAudioSystem()
        {
            EnsureDirectories();
            AudioLibrary library = EnsureAudioLibrary();
            PopulateSoundDefinitions(library);
            AutoAssignExistingClips(library);
            EnsurePlayerPrefabAudioController();
            EnsureAudioManagerPrefab(library);

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green><b>[Shootx Audio]</b> Complete Audio System successfully setup and configured!</color>");
            AuditAudioAssets();
        }

        [MenuItem("Tools/Shootx/Audio/Audit Audio Assets & Missing Clips", false, 11)]
        public static void AuditAudioAssets()
        {
            AudioLibrary library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryAssetPath);
            if (library == null)
            {
                Debug.LogWarning("[Shootx Audio] AudioLibrary asset not found at " + LibraryAssetPath);
                return;
            }

            List<string> missingReports = library.ValidateLibrary();

            Debug.Log($"<b>[Shootx Audio Audit]</b> Total Sound Definitions: {library.Sounds.Count}, Music Tracks: {library.MusicTracks.Count}");
            if (missingReports.Count == 0)
            {
                Debug.Log("<color=green><b>[Shootx Audio Audit]</b> All sound definitions have valid AudioClips assigned!</color>");
            }
            else
            {
                Debug.LogWarning($"<color=orange><b>[Shootx Audio Audit]</b> {missingReports.Count} audio entries require AudioClip assignments:</color>");
                foreach (var report in missingReports)
                {
                    Debug.LogWarning("  • " + report);
                }
            }
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder(AudioRootPath))
            {
                AssetDatabase.CreateFolder("Assets/_Multiplayer", "Audio");
            }
            if (!AssetDatabase.IsValidFolder(LibrariesPath))
            {
                AssetDatabase.CreateFolder(AudioRootPath, "Libraries");
            }
            if (!AssetDatabase.IsValidFolder(MixersPath))
            {
                AssetDatabase.CreateFolder(AudioRootPath, "Mixers");
            }
        }

        private static AudioLibrary EnsureAudioLibrary()
        {
            AudioLibrary library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryAssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryAssetPath);
                Debug.Log("[Shootx Audio] Created new AudioLibrary asset at: " + LibraryAssetPath);
            }
            return library;
        }

        private static void PopulateSoundDefinitions(AudioLibrary library)
        {
            // ─── UI Sounds ───────────────────────────────────────────
            AddOrUpdateSound(library, SoundId.UI_ButtonClick, "UI Button Click", SoundCategory.UI, false, 0f, 0.9f, false);
            AddOrUpdateSound(library, SoundId.UI_ButtonBack, "UI Button Back", SoundCategory.UI, false, 0f, 0.85f, false);
            AddOrUpdateSound(library, SoundId.UI_TabSelect, "UI Tab Select", SoundCategory.UI, false, 0f, 0.8f, false);
            AddOrUpdateSound(library, SoundId.UI_PanelOpen, "UI Panel Open", SoundCategory.UI, false, 0f, 0.75f, false);
            AddOrUpdateSound(library, SoundId.UI_PanelClose, "UI Panel Close", SoundCategory.UI, false, 0f, 0.7f, false);
            AddOrUpdateSound(library, SoundId.UI_ActionSuccess, "UI Action Success", SoundCategory.UI, false, 0f, 0.95f, false);
            AddOrUpdateSound(library, SoundId.UI_ActionFailed, "UI Action Failed / Error", SoundCategory.UI, false, 0f, 0.9f, false);
            AddOrUpdateSound(library, SoundId.UI_LobbyReady, "UI Lobby Ready", SoundCategory.UI, false, 0f, 1.0f, false);
            AddOrUpdateSound(library, SoundId.UI_LobbyUnready, "UI Lobby Unready", SoundCategory.UI, false, 0f, 0.85f, false);
            AddOrUpdateSound(library, SoundId.UI_MatchStartButton, "UI Match Start", SoundCategory.UI, false, 0f, 1.0f, false);
            AddOrUpdateSound(library, SoundId.UI_SliderTick, "UI Slider Tick", SoundCategory.UI, false, 0f, 0.4f, true, 0.95f, 1.05f);
            AddOrUpdateSound(library, SoundId.UI_Toggle, "UI Toggle Switch", SoundCategory.UI, false, 0f, 0.8f, false);

            // ─── Movement ────────────────────────────────────────────
            AddOrUpdateSound(library, SoundId.Movement_Footstep, "Player Footstep", SoundCategory.Movement, true, 1f, 0.85f, true, 0.92f, 1.08f, 1.5f, 20f);
            AddOrUpdateSound(library, SoundId.Movement_Footstep_Dirt, "Player Footstep Dirt", SoundCategory.Movement, true, 1f, 0.85f, true, 0.92f, 1.08f, 1.5f, 20f);
            AddOrUpdateSound(library, SoundId.Movement_Footstep_Metal, "Player Footstep Metal", SoundCategory.Movement, true, 1f, 0.85f, true, 0.92f, 1.08f, 1.5f, 20f);

            // ─── Dash ────────────────────────────────────────────────
            AddOrUpdateSound(library, SoundId.Dash_Whoosh, "Player Dash Whoosh", SoundCategory.Dash, true, 1f, 1.0f, true, 0.95f, 1.05f, 2f, 30f);
            AddOrUpdateSound(library, SoundId.Dash_Impact, "Player Dash Obstacle Impact", SoundCategory.Dash, true, 1f, 0.9f, true, 0.9f, 1.1f, 2f, 25f);

            // ─── Weapons & Impacts ───────────────────────────────────
            AddOrUpdateSound(library, SoundId.Weapon_Revolver_Shoot, "Revolver Gunshot", SoundCategory.Weapons, true, 1f, 1.0f, true, 0.96f, 1.04f, 3f, 40f, 64);
            AddOrUpdateSound(library, SoundId.Weapon_DryFire, "Weapon Dry Fire", SoundCategory.Weapons, true, 1f, 0.85f, true, 0.95f, 1.05f, 1.5f, 15f);
            AddOrUpdateSound(library, SoundId.Weapon_BasicAmmoRefunded, "Basic Ammo Refunded Ping", SoundCategory.Weapons, false, 0f, 0.8f, false);

            AddOrUpdateSound(library, SoundId.Impact_Wall, "Bullet Wall Impact", SoundCategory.Impacts, true, 1f, 0.9f, true, 0.92f, 1.08f, 2f, 30f);
            AddOrUpdateSound(library, SoundId.Impact_Ricochet, "Wall Ricochet Whistle", SoundCategory.Impacts, true, 1f, 0.95f, true, 0.95f, 1.05f, 2f, 35f);
            AddOrUpdateSound(library, SoundId.Impact_DestructibleBox, "Crate Bullet Hit", SoundCategory.Impacts, true, 1f, 0.9f, true, 0.9f, 1.1f, 2f, 30f);
            AddOrUpdateSound(library, SoundId.Impact_PlayerHit, "Bullet Hit Player", SoundCategory.Impacts, true, 1f, 1.0f, true, 0.95f, 1.05f, 2f, 35f);

            // ─── Melee ───────────────────────────────────────────────
            AddOrUpdateSound(library, SoundId.Melee_Swing, "Melee Slash Swing", SoundCategory.Melee, true, 1f, 0.95f, true, 0.95f, 1.05f, 2f, 25f);
            AddOrUpdateSound(library, SoundId.Melee_Hit_Player, "Melee Hit Player", SoundCategory.Melee, true, 1f, 1.0f, true, 0.95f, 1.05f, 2f, 30f);
            AddOrUpdateSound(library, SoundId.Melee_Hit_Environment, "Melee Hit Wall", SoundCategory.Melee, true, 1f, 0.9f, true, 0.9f, 1.1f, 2f, 25f);

            // ─── Destructibles ───────────────────────────────────────
            AddOrUpdateSound(library, SoundId.Box_Destroyed, "Destructible Box Broken", SoundCategory.Environment, true, 1f, 1.0f, true, 0.95f, 1.05f, 2.5f, 35f);

            // ─── Pickups ─────────────────────────────────────────────
            AddOrUpdateSound(library, SoundId.Pickup_Ammo_World, "Ammo Pickup World Collect", SoundCategory.Pickups, true, 1f, 0.9f, false, 1f, 1f, 2f, 25f);
            AddOrUpdateSound(library, SoundId.Pickup_Ammo_Local, "Ammo Pickup Collector Chime", SoundCategory.Pickups, false, 0f, 0.95f, false);

            // ─── Player Lifecycle ────────────────────────────────────
            AddOrUpdateSound(library, SoundId.Player_Hurt, "Player Damage Taken", SoundCategory.SFX, true, 0.8f, 0.95f, true, 0.95f, 1.05f, 2f, 30f);
            AddOrUpdateSound(library, SoundId.Player_Death, "Player Death", SoundCategory.SFX, true, 0.8f, 1.0f, false, 1f, 1f, 2f, 35f, 48);
            AddOrUpdateSound(library, SoundId.Player_Respawn, "Player Respawn", SoundCategory.SFX, true, 0.8f, 0.95f, false, 1f, 1f, 2f, 35f);
            AddOrUpdateSound(library, SoundId.Player_SpawnProtection_Start, "Spawn Protection Active", SoundCategory.SFX, true, 0.8f, 0.85f, false, 1f, 1f, 2f, 25f);
            AddOrUpdateSound(library, SoundId.Player_SpawnProtection_End, "Spawn Protection Expired", SoundCategory.SFX, true, 0.8f, 0.8f, false, 1f, 1f, 2f, 25f);

            // ─── Match Events ────────────────────────────────────────
            AddOrUpdateSound(library, SoundId.Match_CountdownTick, "Pre-Match Countdown Tick", SoundCategory.Announcer, false, 0f, 0.95f, false, 1f, 1f, 1f, 20f, 32);
            AddOrUpdateSound(library, SoundId.Match_CountdownFinal, "Pre-Match Countdown 1", SoundCategory.Announcer, false, 0f, 1.0f, false, 1f, 1f, 1f, 20f, 32);
            AddOrUpdateSound(library, SoundId.Match_Go, "Match Start (GO!)", SoundCategory.Announcer, false, 0f, 1.1f, false, 1f, 1f, 1f, 20f, 24);
            AddOrUpdateSound(library, SoundId.Match_TimerWarning, "Match Timer 30s Warning", SoundCategory.Announcer, false, 0f, 0.9f, false, 1f, 1f, 1f, 20f, 32);
            AddOrUpdateSound(library, SoundId.Match_KillConfirmed, "Local Kill Confirmed Chime", SoundCategory.Announcer, false, 0f, 1.0f, false, 1f, 1f, 1f, 20f, 32);
            AddOrUpdateSound(library, SoundId.Match_Victory, "Match Victory Fanfare", SoundCategory.Announcer, false, 0f, 1.0f, false, 1f, 1f, 1f, 20f, 16);
            AddOrUpdateSound(library, SoundId.Match_Defeat, "Match Defeat Sting", SoundCategory.Announcer, false, 0f, 0.95f, false, 1f, 1f, 1f, 20f, 16);
            AddOrUpdateSound(library, SoundId.Match_Draw, "Match Draw Sting", SoundCategory.Announcer, false, 0f, 0.95f, false, 1f, 1f, 1f, 20f, 16);
            AddOrUpdateSound(library, SoundId.Match_Results_Open, "Results Screen Open Whoosh", SoundCategory.UI, false, 0f, 0.9f, false);

            // ─── Music Tracks ────────────────────────────────────────
            AddOrUpdateMusic(library, MusicTrackId.MainMenu, "Main Menu Theme", 0.75f);
            AddOrUpdateMusic(library, MusicTrackId.Lobby, "Multiplayer Lobby Theme", 0.75f);
            AddOrUpdateMusic(library, MusicTrackId.Gameplay, "Gameplay Arena Ambience", 0.7f);
            AddOrUpdateMusic(library, MusicTrackId.Results, "Match Results Theme", 0.8f);
        }

        private static void AddOrUpdateSound(
            AudioLibrary library,
            SoundId id,
            string displayName,
            SoundCategory category,
            bool is3D,
            float spatialBlend,
            float volume,
            bool randomizePitch,
            float minPitch = 0.95f,
            float maxPitch = 1.05f,
            float minDistance = 2f,
            float maxDistance = 35f,
            int priority = 128)
        {
            if (library.TryGetSound(id, out var existingDef) && existingDef != null)
            {
                existingDef.displayName = displayName;
                existingDef.category = category;
                existingDef.is3D = is3D;
                existingDef.spatialBlend = spatialBlend;
                existingDef.volume = volume;
                existingDef.randomizePitch = randomizePitch;
                existingDef.minPitch = minPitch;
                existingDef.maxPitch = maxPitch;
                existingDef.minDistance = minDistance;
                existingDef.maxDistance = maxDistance;
                existingDef.priority = priority;
                return;
            }

            var def = new SoundDefinition
            {
                id = id,
                displayName = displayName,
                category = category,
                is3D = is3D,
                spatialBlend = spatialBlend,
                volume = volume,
                randomizePitch = randomizePitch,
                minPitch = minPitch,
                maxPitch = maxPitch,
                minDistance = minDistance,
                maxDistance = maxDistance,
                priority = priority,
                rolloffMode = AudioRolloffMode.Logarithmic
            };
            library.SetSoundDefinition(def);
        }

        private static void AddOrUpdateMusic(AudioLibrary library, MusicTrackId trackId, string title, float volume)
        {
            if (library.TryGetMusic(trackId, out var existingTrack) && existingTrack != null)
            {
                existingTrack.title = title;
                existingTrack.volume = volume;
                return;
            }

            var track = new MusicTrackDefinition
            {
                trackId = trackId,
                title = title,
                volume = volume,
                loop = true
            };
            library.SetMusicTrack(track);
        }

        private static void AutoAssignExistingClips(AudioLibrary library)
        {
            // Map discovered project audio files to sound definitions
            TryAssignClips(library, SoundId.UI_ButtonClick, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Click01.wav", "Assets/Buttons, Slides, Dings and Drops/Audio/UI/ButtonNoise02.wav");
            TryAssignClips(library, SoundId.UI_ButtonBack, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/ButtonNoise04.wav");
            TryAssignClips(library, SoundId.UI_TabSelect, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/ButtonNoise03.wav");
            TryAssignClips(library, SoundId.UI_PanelOpen, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Chord04.wav");
            TryAssignClips(library, SoundId.UI_PanelClose, "Assets/GUIPackCartoon/Demo/Sounds/Close.wav");
            TryAssignClips(library, SoundId.UI_ActionSuccess, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Chord01.wav");
            TryAssignClips(library, SoundId.UI_ActionFailed, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Alert01.wav");
            TryAssignClips(library, SoundId.UI_LobbyReady, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Chord05.wav");
            TryAssignClips(library, SoundId.UI_LobbyUnready, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/ButtonNoise06.wav");
            TryAssignClips(library, SoundId.UI_MatchStartButton, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Chord08.wav");
            TryAssignClips(library, SoundId.UI_SliderTick, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Chirp01.wav");
            TryAssignClips(library, SoundId.UI_Toggle, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Stacc01.wav");

            TryAssignClips(library, SoundId.Weapon_Revolver_Shoot, "Assets/Buttons, Slides, Dings and Drops/Audio/bulletshot.mp3", "Assets/Vefects/Flipbook VFX/Audio/WAV/Vefects_SFX_GunShot_01.wav", "Assets/Vefects/Flipbook VFX/Audio/WAV/Vefects_SFX_GunShot_02.wav");
            TryAssignClips(library, SoundId.Weapon_DryFire, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Press01.wav");
            TryAssignClips(library, SoundId.Weapon_BasicAmmoRefunded, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/FairyBell_01.wav");

            TryAssignClips(library, SoundId.Impact_Wall, "Assets/Buttons, Slides, Dings and Drops/Audio/bullet-hit.mp3", "Assets/Vefects/Flipbook VFX/Audio/WAV/Vefects_SFX_Impact_01.wav");
            TryAssignClips(library, SoundId.Impact_Ricochet, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Scrape01.wav");
            TryAssignClips(library, SoundId.Impact_DestructibleBox, "Assets/Vefects/Flipbook VFX/Audio/WAV/Vefects_SFX_Shrapnel_01.wav");
            TryAssignClips(library, SoundId.Impact_PlayerHit, "Assets/Vefects/Flipbook VFX/Audio/WAV/Vefects_SFX_Impact_Blood_01.wav");

            TryAssignClips(library, SoundId.Melee_Swing, "Assets/Vefects/Flipbook VFX/Audio/WAV/Vefects_SFX_Slash_Classic.wav");
            TryAssignClips(library, SoundId.Melee_Hit_Player, "Assets/Vefects/Flipbook VFX/Audio/WAV/Vefects_SFX_Impact_Blood_01.wav");

            TryAssignClips(library, SoundId.Dash_Whoosh, "Assets/Sci-Fi Weapons-Bullet Hell Sound Effects Pack/AUDIO/Skill/SFX_SCIFI_ENERGY_Skill_Whoosh_Electrified_1.wav");
            TryAssignClips(library, SoundId.Box_Destroyed, "Assets/Vefects/Flipbook VFX/Audio/WAV/Vefects_SFX_Explosion_Classic.wav");

            TryAssignClips(library, SoundId.Pickup_Ammo_World, "Assets/GUIPackCartoon/Demo/Sounds/Coins.wav");
            TryAssignClips(library, SoundId.Pickup_Ammo_Local, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Chord02.wav");

            TryAssignClips(library, SoundId.Match_CountdownTick, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Button Noise1.wav");
            TryAssignClips(library, SoundId.Match_CountdownFinal, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Chord03.wav");
            TryAssignClips(library, SoundId.Match_Go, "Assets/GUIPackCartoon/Demo/Sounds/Magical.wav");
            TryAssignClips(library, SoundId.Match_TimerWarning, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Alert02.wav");
            TryAssignClips(library, SoundId.Match_KillConfirmed, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Harmonic01.wav");
            TryAssignClips(library, SoundId.Match_Victory, "Assets/GUIPackCartoon/Demo/Sounds/Win.wav");
            TryAssignClips(library, SoundId.Match_Defeat, "Assets/GUIPackCartoon/Demo/Sounds/Lose - Thunder.wav");
            TryAssignClips(library, SoundId.Match_Draw, "Assets/Buttons, Slides, Dings and Drops/Audio/UI/Chord07.wav");

            // Music
            TryAssignMusicClip(library, MusicTrackId.MainMenu, "Assets/Buttons, Slides, Dings and Drops/Burgundy - All Night (freetouse.com).mp3");
            TryAssignMusicClip(library, MusicTrackId.Lobby, "Assets/GUIPackCartoon/Demo/Sounds/Background Music.wav");
            TryAssignMusicClip(library, MusicTrackId.Gameplay, "Assets/Buttons, Slides, Dings and Drops/Aetheric - Edge of Motion (freetouse.com).mp3");
            TryAssignMusicClip(library, MusicTrackId.Results, "Assets/Buttons, Slides, Dings and Drops/Audio/Dagored - Wall Of Sound.mp3");
        }

        private static void TryAssignClips(AudioLibrary library, SoundId id, params string[] relativePaths)
        {
            if (!library.TryGetSound(id, out var def) || def == null) return;
            if (def.HasValidClips) return; // Do not overwrite user-customized clips

            List<AudioClip> foundClips = new List<AudioClip>();
            foreach (var path in relativePaths)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    foundClips.Add(clip);
                }
            }

            if (foundClips.Count > 0)
            {
                def.clips = foundClips.ToArray();
            }
        }

        private static void TryAssignMusicClip(AudioLibrary library, MusicTrackId trackId, string relativePath)
        {
            if (!library.TryGetMusic(trackId, out var track) || track == null) return;
            if (track.clip != null) return;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(relativePath);
            if (clip != null)
            {
                track.clip = clip;
            }
        }

        private static void EnsurePlayerPrefabAudioController()
        {
            string playerPrefabPath = "Assets/_Multiplayer/Players/Prefabs/Player.prefab";
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (playerPrefab == null) return;

            if (!playerPrefab.TryGetComponent<PlayerAudioController>(out _))
            {
                playerPrefab.AddComponent<PlayerAudioController>();
                EditorUtility.SetDirty(playerPrefab);
                Debug.Log("[Shootx Audio] Added PlayerAudioController to Player.prefab");
            }
        }

        private static void EnsureAudioManagerPrefab(AudioLibrary library)
        {
            string prefabPath = "Assets/_Multiplayer/Resources/AudioManager.prefab";
            if (!AssetDatabase.IsValidFolder("Assets/_Multiplayer/Resources"))
            {
                AssetDatabase.CreateFolder("Assets/_Multiplayer", "Resources");
            }

            GameObject go = new GameObject("AudioManager");
            var audioMgr = go.AddComponent<Shootx.Audio.AudioManager>();

            SerializedObject so = new SerializedObject(audioMgr);
            so.FindProperty("audioLibrary").objectReferenceValue = library;

            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/_Multiplayer/Audio/Mixers/GameAudioMixer.mixer");
            if (mixer != null)
            {
                so.FindProperty("audioMixer").objectReferenceValue = mixer;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("[Shootx Audio] Created/Updated AudioManager.prefab in Resources!");
        }
    }
}
#endif
