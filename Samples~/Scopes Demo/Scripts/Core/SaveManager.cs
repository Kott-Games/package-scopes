using System;
using System.Collections;
using Kott.Utilities.Progress;
using UnityEngine;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     Loads the profile when Core initializes and saves it on every change and when Core unloads. Lives in the
    ///     root scope, so every scope below can inject it.
    /// </summary>
    [Serializable]
    public sealed class SaveManager : Manager, ISaveManager
    {
        [SerializeField]
        [Tooltip("PlayerPrefs key of the profile.")]
        private string _storageKey = "Kott.Scopes.Demo.Profile";

        /// <inheritdoc />
        public PlayerProfile Profile { get; private set; }

        /// <inheritdoc />
        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            string json = PlayerPrefs.GetString(_storageKey, string.Empty);
            Profile = string.IsNullOrEmpty(json) ? new PlayerProfile() : JsonUtility.FromJson<PlayerProfile>(json);
            yield break;
        }

        /// <inheritdoc />
        public override IEnumerator Uninitialize(IProgress<ProgressReport> progress)
        {
            // Uninitialize may span frames, e.g. to wait for a cloud save before the scope is gone
            Save();
            yield break;
        }

        /// <inheritdoc />
        public void RecordGame(int coins)
        {
            Profile.GamesPlayed++;
            Profile.TotalCoins += coins;
            Save();
        }

        /// <inheritdoc />
        public bool SubmitScore(string miniGame, int score)
        {
            BestScore entry = Profile.BestScores.Find(best => best.MiniGame == miniGame);
            if (entry == null)
            {
                Profile.BestScores.Add(new BestScore { MiniGame = miniGame, Score = score });
            }
            else if (score > entry.Score)
            {
                entry.Score = score;
            }
            else
            {
                return false;
            }

            Save();
            return true;
        }

        /// <inheritdoc />
        public void ResetProfile()
        {
            Profile = new PlayerProfile();
            Save();
        }

        private void Save()
        {
            PlayerPrefs.SetString(_storageKey, JsonUtility.ToJson(Profile));
            PlayerPrefs.Save();
        }
    }
}
