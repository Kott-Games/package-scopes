using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace Kott.Scopes.Demo.Tests
{
    /// <summary>
    ///     Plays through the demo's scope tree with real scenes: Core → Menu → Game/Board → Reaction → Board → Menu.
    ///     Needs the demo's scenes registered (Tools > Scopes > Validate All, then the Fix buttons).
    /// </summary>
    public class DemoSmokeTest
    {
        private const float Timeout = 20f;
        private const string ProfileKey = "Kott.Scopes.Demo.Profile";

        private readonly Dictionary<string, ScopeDefinition> _definitions = new();
        private string _savedProfile;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            // the sample can be imported anywhere: find its definitions next to its runtime assembly
            string asmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName("Kott.Scopes.Demo");
            string scopesFolder = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(asmdef)), "Scopes")
                .Replace('\\', '/');
            foreach (string guid in AssetDatabase.FindAssets("t:ScopeDefinition", new[] { scopesFolder }))
            {
                ScopeDefinition definition =
                    AssetDatabase.LoadAssetAtPath<ScopeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                _definitions[definition.name] = definition;
            }
#else
            Assert.Ignore("The demo smoke test finds the demo's assets through the AssetDatabase: editor only.");
#endif
            Assume.That(ScopeHandler.Instance.RootScope, Is.Null, "Another scope tree is loaded.");

            // the demo saves its profile in PlayerPrefs: keep the player's one
            _savedProfile = PlayerPrefs.HasKey(ProfileKey) ? PlayerPrefs.GetString(ProfileKey) : null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ScopeHandler handler = ScopeHandler.Instance;
            if (handler.RootScope != null)
            {
                ScopeOperation unload = handler.UnloadScope(handler.RootScope);
                yield return unload;
            }

            if (_savedProfile != null)
            {
                PlayerPrefs.SetString(ProfileKey, _savedProfile);
            }
            else
            {
                PlayerPrefs.DeleteKey(ProfileKey);
            }

            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator Demo_MenuGameMiniGameAndBack_TreeMatchesEveryStep()
        {
            ScopeHandler handler = ScopeHandler.Instance;

            // Core queues Menu itself (AppFlow.Initialize)
            handler.LoadScope(Definition("Core"));
            yield return WaitUntil(() => handler.IsLoaded(Definition("Menu")), "Menu loaded");
            AssertTree("Core(Menu)");

            IAppFlow app = handler.RootScope.Resolve<IAppFlow>();
            yield return WaitUntil(() => !app.IsBusy, "app flow idle");
            app.StartGame();
            yield return WaitUntil(() => handler.IsLoaded(Definition("Board")), "Board loaded");
            AssertTree("Core(Game(Board))");

            Assert.That(handler.TryGetScope(Definition("Game"), out Scope game), Is.True);
            IGameFlow flow = game.Resolve<IGameFlow>();
            Assert.That(game.Resolve<IGameSession>().Players, Has.Count.EqualTo(2));

            yield return WaitUntil(() => !flow.IsBusy, "game flow idle");
            flow.PlayMiniGame(Definition("Reaction"));
            yield return WaitUntil(() => handler.IsLoaded(Definition("Reaction")) && !flow.IsBusy, "Reaction loaded");
            AssertTree("Core(Game(Reaction))");
            Assert.That(flow.CurrentMiniGame.DisplayName, Is.EqualTo("Reaction"));

            flow.ReturnToBoard();
            yield return WaitUntil(() => handler.IsLoaded(Definition("Board")) && !flow.IsBusy, "Board loaded again");
            AssertTree("Core(Game(Board))");

            app.ShowMenu();
            yield return WaitUntil(() => handler.IsLoaded(Definition("Menu")) && !app.IsBusy, "Menu loaded again");
            AssertTree("Core(Menu)");
        }

        private ScopeDefinition Definition(string name)
        {
            Assert.That(_definitions.TryGetValue(name, out ScopeDefinition definition), Is.True,
                $"No scope definition '{name}' in the demo.");
            return definition;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > Timeout)
                {
                    Assert.Fail($"Timed out waiting for: {what}. Tree: {Describe(ScopeHandler.Instance.RootScope)}");
                }

                yield return null;
            }
        }

        private static void AssertTree(string expected)
        {
            Assert.That(Describe(ScopeHandler.Instance.RootScope), Is.EqualTo(expected));
        }

        // "Core(Game(Board))"; only loaded scopes
        private static string Describe(Scope scope)
        {
            if (scope == null)
            {
                return "<none>";
            }

            string[] children = scope.Children.Where(child => child.State == ScopeState.Loaded).Reverse()
                .Select(Describe).ToArray();
            return children.Length == 0 ? scope.Name : $"{scope.Name}({string.Join(",", children)})";
        }
    }
}
