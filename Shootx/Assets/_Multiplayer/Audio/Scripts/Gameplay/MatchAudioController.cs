using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Shootx.Audio
{
    /// <summary>
    /// Coordinates audio events with MatchManager lifecycle:
    /// countdown ticks, GO signal, timer warning, kill confirmations,
    /// and victory / defeat / draw fanfare.
    /// </summary>
    public class MatchAudioController : MonoBehaviour
    {
        private bool _isSubscribed = false;
        private int _lastCountdownValue = -1;
        private bool _timerWarningPlayed = false;

        private void OnEnable()
        {
            StartCoroutine(BindMatchManagerCoroutine());
        }

        private void OnDisable()
        {
            UnbindMatchManager();
        }

        private IEnumerator BindMatchManagerCoroutine()
        {
            while (MatchManager.Instance == null)
            {
                yield return null;
            }

            BindMatchManager();
        }

        private void BindMatchManager()
        {
            if (_isSubscribed || MatchManager.Instance == null) return;
            _isSubscribed = true;

            MatchManager.Instance.OnCountdownChanged += HandleCountdownChanged;
            MatchManager.Instance.OnMatchGoSignal += HandleMatchGo;
            MatchManager.Instance.OnMatchTimeUpdated += HandleMatchTimeUpdated;
            MatchManager.Instance.OnResultsReady += HandleResultsReady;
        }

        private void UnbindMatchManager()
        {
            if (!_isSubscribed) return;
            _isSubscribed = false;

            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnCountdownChanged -= HandleCountdownChanged;
                MatchManager.Instance.OnMatchGoSignal -= HandleMatchGo;
                MatchManager.Instance.OnMatchTimeUpdated -= HandleMatchTimeUpdated;
                MatchManager.Instance.OnResultsReady -= HandleResultsReady;
            }
        }

        private void HandleCountdownChanged(int count)
        {
            if (count <= 0 || count == _lastCountdownValue) return;
            _lastCountdownValue = count;

            if (count == 1)
            {
                AudioManager.Instance?.PlaySound2D(SoundId.Match_CountdownFinal, 1.0f);
            }
            else
            {
                AudioManager.Instance?.PlaySound2D(SoundId.Match_CountdownTick, 0.9f);
            }
        }

        private void HandleMatchGo()
        {
            AudioManager.Instance?.PlaySound2D(SoundId.Match_Go, 1.1f);
            _timerWarningPlayed = false;
        }

        private void HandleMatchTimeUpdated(float remaining)
        {
            if (!_timerWarningPlayed && remaining <= 30f && remaining > 0.5f)
            {
                _timerWarningPlayed = true;
                AudioManager.Instance?.PlaySound2D(SoundId.Match_TimerWarning, 0.9f);
            }
        }

        public void PlayKillConfirmedFeedback()
        {
            AudioManager.Instance?.PlaySound2D(SoundId.Match_KillConfirmed, 1.0f);
        }

        private void HandleResultsReady(MatchResultData result)
        {
            if (result == null) return;

            AudioManager.Instance?.PlaySound2D(SoundId.Match_Results_Open, 0.9f);

            ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
            bool localWon = false;
            bool localDraw = false;

            if (result.IsDraw)
            {
                var entry = result.Entries.Find(e => e.ClientId == localClientId);
                localDraw = entry != null && entry.Rank == 1;
                localWon = localDraw;
            }
            else
            {
                localWon = result.WinnerClientId == localClientId;
            }

            if (localWon && !result.IsDraw)
            {
                AudioManager.Instance?.PlaySound2D(SoundId.Match_Victory, 1.0f);
            }
            else if (result.IsDraw && localDraw)
            {
                AudioManager.Instance?.PlaySound2D(SoundId.Match_Draw, 1.0f);
            }
            else
            {
                AudioManager.Instance?.PlaySound2D(SoundId.Match_Defeat, 0.9f);
            }

            // Crossfade into Results music
            AudioManager.Instance?.PlayMusic(MusicTrackId.Results, 1.0f);
        }
    }
}
