using System;
using System.Collections;
using System.Text;
using Kott.Utilities.Progress;
using UnityEngine;
using UnityEngine.UI;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     The Stats scope: scene-less, its panel is spawned under the scope's root and disappears when the scope
    ///     unloads. Reads the profile from the root scope.
    /// </summary>
    [Serializable]
    public sealed class StatsPanel : Manager
    {
        [Inject]
        private ISaveManager _save;

        private Text _text;
        private bool _closing;

        /// <inheritdoc />
        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            RectTransform canvas = DemoUI.CreateCanvas("Stats", Scope.Root.transform, 10);
            RectTransform column = DemoUI.Column(canvas, 560f, DemoUI.PanelColor);
            column.anchorMin = column.anchorMax = column.pivot = new Vector2(0f, 0.5f);
            column.anchoredPosition = new Vector2(48f, 0f);

            DemoUI.Label(column, "<b>Stats</b>", 48).color = DemoUI.AccentColor;
            _text = DemoUI.Label(column, string.Empty, 28, TextAnchor.UpperLeft);
            RectTransform row = DemoUI.Row(column);
            DemoUI.Button(row, "Reset", ResetProfile, 28);
            DemoUI.Button(row, "Close", Close, 28);
            Refresh();
            yield break;
        }

        private void Refresh()
        {
            PlayerProfile profile = _save.Profile;
            StringBuilder builder = new();
            builder.Append("Games played: ").Append(profile.GamesPlayed).Append('\n');
            builder.Append("Coins collected: ").Append(profile.TotalCoins).Append("\n\n<b>Best scores</b>\n");
            if (profile.BestScores.Count == 0)
            {
                builder.Append("<color=#8a93a6>no mini game played yet</color>");
            }

            foreach (BestScore best in profile.BestScores)
            {
                builder.Append(best.MiniGame).Append(": ").Append(best.Score).Append('\n');
            }

            _text.text = builder.ToString().TrimEnd();
        }

        private void ResetProfile()
        {
            _save.ResetProfile();
            Refresh();
        }

        private void Close()
        {
            if (!_closing)
            {
                _closing = true;
                ScopeHandler.Instance.UnloadScope(Scope);
            }
        }
    }
}
