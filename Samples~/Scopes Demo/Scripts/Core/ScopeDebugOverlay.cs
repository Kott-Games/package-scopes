using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Kott.Utilities.Progress;
using UnityEngine;
using UnityEngine.UI;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     Draws the scope tree as it is right now (state, scene, provided contracts) and logs the
    ///     <see cref="ScopeHandler.ScopeLoaded" /> / <see cref="ScopeHandler.ScopeUnloaded" /> events. F1 toggles it.
    /// </summary>
    [Serializable]
    public sealed class ScopeDebugOverlay : Manager, IScopeDebugOverlay
    {
        [SerializeField]
        private bool _visibleOnStart = true;

        [SerializeField]
        private KeyCode _toggleKey = KeyCode.F1;

        [SerializeField]
        private int _logLines = 14;

        [SerializeField]
        private float _toastDuration = 4f;

        private readonly Queue<string> _log = new();
        private readonly StringBuilder _builder = new();
        private RectTransform _panel;
        private Text _text;
        private RectTransform _toast;
        private Text _toastText;
        private float _toastHiddenAt;

        /// <inheritdoc />
        public override IEnumerator Initialize(IProgress<ProgressReport> progress)
        {
            RectTransform canvas = DemoUI.CreateCanvas("Scope Debug Overlay", Scope.Root.transform, 200);

            _panel = DemoUI.Panel(canvas, new Color(0f, 0f, 0f, 0.6f), new Vector2(0.69f, 0f), Vector2.one, "Tree");
            _panel.GetComponent<Image>().raycastTarget = false;
            _text = DemoUI.Label(_panel, string.Empty, 20, TextAnchor.UpperLeft);
            DemoUI.Stretch((RectTransform)_text.transform);
            ((RectTransform)_text.transform).offsetMin = new Vector2(16f, 16f);
            ((RectTransform)_text.transform).offsetMax = new Vector2(-16f, -16f);
            _panel.gameObject.SetActive(_visibleOnStart);

            _toast = DemoUI.Panel(canvas, new Color(0.6f, 0.12f, 0.12f, 0.95f), new Vector2(0.2f, 0.88f),
                new Vector2(0.68f, 0.98f), "Toast");
            _toast.GetComponent<Image>().raycastTarget = false;
            _toastText = DemoUI.Label(_toast, string.Empty, 24);
            DemoUI.Stretch((RectTransform)_toastText.transform);
            _toast.gameObject.SetActive(false);

            // the handler outlives every scope: unsubscribe in Uninitialize
            ScopeHandler.Instance.ScopeLoaded += OnScopeLoaded;
            ScopeHandler.Instance.ScopeUnloaded += OnScopeUnloaded;
            Log("Overlay ready (F1 toggles it)");
            yield break;
        }

        /// <inheritdoc />
        public override IEnumerator Uninitialize(IProgress<ProgressReport> progress)
        {
            if (ScopeHandler.HasInstance)
            {
                ScopeHandler.Instance.ScopeLoaded -= OnScopeLoaded;
                ScopeHandler.Instance.ScopeUnloaded -= OnScopeUnloaded;
            }

            yield break;
        }

        /// <inheritdoc />
        public override void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _panel.gameObject.SetActive(!_panel.gameObject.activeSelf);
            }

            if (_toast.gameObject.activeSelf && Time.unscaledTime >= _toastHiddenAt)
            {
                _toast.gameObject.SetActive(false);
            }

            if (_panel.gameObject.activeSelf)
            {
                Redraw();
            }
        }

        /// <inheritdoc />
        public void Log(string message)
        {
            _log.Enqueue($"<color=#8a93a6>{Time.unscaledTime,6:0.0}</color>  {message}");
            while (_log.Count > _logLines)
            {
                _log.Dequeue();
            }
        }

        /// <inheritdoc />
        public void ShowError(string message)
        {
            Log($"<color=#ff7070>{message}</color>");
            _toastText.text = message;
            _toast.gameObject.SetActive(true);
            _toastHiddenAt = Time.unscaledTime + _toastDuration;
        }

        private void OnScopeLoaded(Scope scope)
        {
            Log($"<color=#7fd67f>loaded</color>   {scope.Name}");
        }

        private void OnScopeUnloaded(Scope scope)
        {
            Log($"<color=#e0b060>unloaded</color> {scope.Name}");
        }

        private void Redraw()
        {
            _builder.Clear();
            _builder.Append("<b>SCOPES</b>  <size=16>(F1)</size>\n");
            AppendScope(ScopeHandler.Instance.RootScope, 0);
            _builder.Append("\n<b>LIFECYCLE</b>\n");
            foreach (string line in _log)
            {
                _builder.Append(line).Append('\n');
            }

            _text.text = _builder.ToString();
        }

        private void AppendScope(Scope scope, int depth)
        {
            if (scope == null)
            {
                return;
            }

            _builder.Append(' ', depth * 4).Append("<b>").Append(scope.Name).Append("</b> ");
            _builder.Append(scope.State == ScopeState.Loaded
                ? "<color=#7fd67f>Loaded</color>"
                : $"<color=#e0b060>{scope.State}</color>");
            if (scope.Scene.IsValid())
            {
                _builder.Append("  <color=#8ab4ff>scene ").Append(scope.Scene.name).Append("</color>");
            }

            _builder.Append('\n');
            foreach (Manager manager in scope.Managers)
            {
                // null while a load with a missing manager type fails
                if (manager == null)
                {
                    continue;
                }

                foreach (Type contract in manager.GetType().GetInterfaces())
                {
                    if (contract != typeof(IManagerContract) && typeof(IManagerContract).IsAssignableFrom(contract))
                    {
                        _builder.Append(' ', depth * 4 + 2).Append("<size=16><color=#8a93a6>provides ")
                            .Append(contract.Name).Append("</color></size>\n");
                    }
                }
            }

            // Children lists the most recently loaded scope first
            for (int i = scope.Children.Count - 1; i >= 0; i--)
            {
                AppendScope(scope.Children[i], depth + 1);
            }
        }
    }
}
