using System;
using System.Collections.Generic;
using System.Text;
using Object = UnityEngine.Object;

namespace Kott.Scopes
{
    /// <summary>How serious a <see cref="ScopeIssue" /> is.</summary>
    public enum ScopeIssueSeverity
    {
        /// <summary>Probably a mistake, but the scope can still load (e.g. a dependency the actual parent may provide).</summary>
        Warning,

        /// <summary>Loading the scope fails or misbehaves.</summary>
        Error,
    }

    /// <summary>The rule a <see cref="ScopeIssue" /> violates.</summary>
    public enum ScopeIssueKind
    {
        /// <summary>A manager entry is <c>null</c>.</summary>
        EmptyManagerSlot,

        /// <summary>A manager's stored type no longer exists (renamed, moved or deleted class).</summary>
        UnresolvedManagerType,

        /// <summary>A manager requires a type that can never be resolved as a singleton.</summary>
        InvalidRequirement,

        /// <summary>A required singleton is provided neither by the scope nor by its declared parents.</summary>
        MissingSingleton,

        /// <summary>A singleton type is provided twice within the scope and its declared parents.</summary>
        DuplicateSingleton,

        /// <summary>The declared parents form a cycle.</summary>
        DeclaredParentCycle,

        /// <summary>The referenced scene asset does not exist.</summary>
        SceneNotFound,

        /// <summary>The scene is not registered with its loader (build profile scene list, Addressables).</summary>
        SceneNotRegistered,

        /// <summary>The scene has no <see cref="SceneRoot" />.</summary>
        SceneWithoutSceneRoot,

        /// <summary>The scene has more than one <see cref="SceneRoot" />.</summary>
        MultipleSceneRoots,

        /// <summary>Two <see cref="SceneLink" />s in the scene share a key.</summary>
        DuplicateSceneLinkKey,

        /// <summary>A <see cref="SceneLink" /> has no key.</summary>
        EmptySceneLinkKey,
    }

    /// <summary>One problem found by <see cref="ScopeValidator" />.</summary>
    public sealed class ScopeIssue
    {
        internal ScopeIssue(Scope scope, ScopeIssueKind kind, ScopeIssueSeverity severity, string message,
            Object context = null)
        {
            Scope = scope;
            Kind = kind;
            Severity = severity;
            Message = message;
            Context = context != null ? context : scope;
        }

        /// <summary>The validated scope.</summary>
        public Scope Scope { get; }

        /// <summary>The violated rule.</summary>
        public ScopeIssueKind Kind { get; }

        /// <summary>How serious the issue is.</summary>
        public ScopeIssueSeverity Severity { get; }

        /// <summary>Human-readable description, including how to fix it.</summary>
        public string Message { get; }

        /// <summary>The asset to select to fix the issue: the scope, or its scene.</summary>
        public Object Context { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"[{Severity}] {(Scope != null ? Scope.name : "?")}: {Message}";
        }
    }

    /// <summary>A singleton the managers of a scope require, and which scope provides it.</summary>
    public sealed class SingletonRequirement
    {
        private readonly List<Type> _requiredBy = new();

        internal SingletonRequirement(Type type)
        {
            Type = type;
        }

        /// <summary>The required singleton type.</summary>
        public Type Type { get; }

        /// <summary>The manager types that require it.</summary>
        public IReadOnlyList<Type> RequiredBy => _requiredBy;

        /// <summary>The scope (this one or a declared parent) providing it, or <c>null</c>.</summary>
        public Scope ProvidedBy { get; internal set; }

        /// <summary>The manager type providing it, or <c>null</c>.</summary>
        public Type Provider { get; internal set; }

        /// <summary>Whether the scope or one of its declared parents provides it.</summary>
        public bool IsProvided => Provider != null;

        internal void AddRequiredBy(Type managerType)
        {
            if (!_requiredBy.Contains(managerType))
            {
                _requiredBy.Add(managerType);
            }
        }
    }

    /// <summary>The result of validating one scope with <see cref="ScopeValidator" />.</summary>
    public sealed class ScopeValidationResult
    {
        internal ScopeValidationResult(Scope scope, List<ScopeIssue> issues, List<SingletonRequirement> requirements,
            bool sceneInspected)
        {
            Scope = scope;
            Issues = issues;
            Requirements = requirements;
            SceneInspected = sceneInspected;
        }

        /// <summary>The validated scope.</summary>
        public Scope Scope { get; }

        /// <summary>All problems found, in rule order.</summary>
        public IReadOnlyList<ScopeIssue> Issues { get; }

        /// <summary>The singletons the scope's managers require and where they come from.</summary>
        public IReadOnlyList<SingletonRequirement> Requirements { get; }

        /// <summary>Whether the scene's content (SceneRoot, SceneLinks) was checked.</summary>
        public bool SceneInspected { get; }

        /// <summary>Whether any issue is an <see cref="ScopeIssueSeverity.Error" />.</summary>
        public bool HasErrors
        {
            get
            {
                foreach (ScopeIssue issue in Issues)
                {
                    if (issue.Severity == ScopeIssueSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            StringBuilder builder = new();
            builder.Append($"Scope '{(Scope != null ? Scope.name : "?")}': {Issues.Count} issue(s)");
            foreach (ScopeIssue issue in Issues)
            {
                builder.Append($"\n  [{issue.Severity}] {issue.Message}");
            }

            return builder.ToString();
        }
    }
}
