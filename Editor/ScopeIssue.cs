using System;
using System.Collections.Generic;
using System.Text;
using Object = UnityEngine.Object;

namespace Kott.Scopes
{
    /// <summary>How serious a <see cref="ScopeIssue" /> is.</summary>
    public enum ScopeIssueSeverity
    {
        /// <summary>Probably a mistake, but the scope can still load (e.g. a contract the actual parent may provide).</summary>
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

        /// <summary>
        ///     An injected field can never be filled: an <see cref="InjectAttribute" /> type that is not a manager
        ///     contract, or an <see cref="InjectSceneAttribute" /> with an empty key or unsupported type.
        /// </summary>
        InvalidInjection,

        /// <summary>A required contract is provided neither by the scope nor by its declared parents.</summary>
        MissingContract,

        /// <summary>A contract is provided twice within the scope and its declared parents.</summary>
        DuplicateContract,

        /// <summary>Managers of the scope inject each other in a cycle, so there is no initialization order.</summary>
        DependencyCycle,

        /// <summary>The declared parents form a cycle.</summary>
        DeclaredParentCycle,

        /// <summary>A manager has required <see cref="InjectSceneAttribute" /> fields, but the scope has no scene.</summary>
        SceneInjectionWithoutScene,

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

        /// <summary>An <see cref="InjectSceneAttribute" /> key has no <see cref="SceneLink" /> in the scene.</summary>
        MissingSceneLink,

        /// <summary>The <see cref="SceneLink" /> for an <see cref="InjectSceneAttribute" /> field lacks the field's component.</summary>
        MissingSceneLinkComponent,
    }

    /// <summary>One problem found by <see cref="ScopeValidator" />.</summary>
    public sealed class ScopeIssue
    {
        private readonly Action _fix;

        internal ScopeIssue(ScopeDefinition definition, ScopeIssueKind kind, ScopeIssueSeverity severity,
            string message, Object context = null, string fixLabel = null, Action fix = null)
        {
            Definition = definition;
            Kind = kind;
            Severity = severity;
            Message = message;
            Context = context != null ? context : definition;
            FixLabel = fix != null ? fixLabel ?? "Fix" : null;
            _fix = fix;
        }

        /// <summary>The validated definition.</summary>
        public ScopeDefinition Definition { get; }

        /// <summary>The violated rule.</summary>
        public ScopeIssueKind Kind { get; }

        /// <summary>How serious the issue is.</summary>
        public ScopeIssueSeverity Severity { get; }

        /// <summary>Human-readable description, including how to fix it.</summary>
        public string Message { get; }

        /// <summary>The asset to select to fix the issue: the definition, or its scene.</summary>
        public Object Context { get; }

        /// <summary>Whether <see cref="Fix" /> can resolve the issue automatically.</summary>
        public bool CanFix => _fix != null;

        /// <summary>Button text for <see cref="Fix" />, e.g. "Add to Build Profile"; <c>null</c> if not fixable.</summary>
        public string FixLabel { get; }

        /// <summary>Resolves the issue (with undo where possible). Validate again afterwards.</summary>
        /// <exception cref="InvalidOperationException">The issue cannot be fixed automatically.</exception>
        public void Fix()
        {
            if (_fix == null)
            {
                throw new InvalidOperationException($"{Kind} issues cannot be fixed automatically.");
            }

            _fix();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"[{Severity}] {(Definition != null ? Definition.name : "?")}: {Message}";
        }
    }

    /// <summary>A contract the managers of a scope inject, and which scope provides it.</summary>
    public sealed class ContractRequirement
    {
        private readonly List<Type> _requiredBy = new();

        internal ContractRequirement(Type type)
        {
            Type = type;
            IsOptional = true;
        }

        /// <summary>The injected contract.</summary>
        public Type Type { get; }

        /// <summary>The manager types that inject it.</summary>
        public IReadOnlyList<Type> RequiredBy => _requiredBy;

        /// <summary>Whether every field injecting it is optional.</summary>
        public bool IsOptional { get; private set; }

        /// <summary>The definition (this one or a declared parent) providing it, or <c>null</c>.</summary>
        public ScopeDefinition ProvidedBy { get; internal set; }

        /// <summary>The manager type providing it, or <c>null</c>.</summary>
        public Type Provider { get; internal set; }

        /// <summary>Whether the scope or one of its declared parents provides it.</summary>
        public bool IsProvided => Provider != null;

        internal void AddRequiredBy(Type managerType, bool optional)
        {
            if (!_requiredBy.Contains(managerType))
            {
                _requiredBy.Add(managerType);
            }

            IsOptional &= optional;
        }
    }

    /// <summary>The result of validating one definition with <see cref="ScopeValidator" />.</summary>
    public sealed class ScopeValidationResult
    {
        internal ScopeValidationResult(ScopeDefinition definition, List<ScopeIssue> issues,
            List<ContractRequirement> requirements, List<int> initializationOrder, bool sceneInspected)
        {
            Definition = definition;
            Issues = issues;
            Requirements = requirements;
            InitializationOrder = initializationOrder;
            SceneInspected = sceneInspected;
        }

        /// <summary>The validated definition.</summary>
        public ScopeDefinition Definition { get; }

        /// <summary>All problems found, in rule order.</summary>
        public IReadOnlyList<ScopeIssue> Issues { get; }

        /// <summary>The contracts the scope's managers inject and where they come from.</summary>
        public IReadOnlyList<ContractRequirement> Requirements { get; }

        /// <summary>
        ///     Indices into <see cref="ScopeDefinition.Managers" /> in the order the managers initialize; empty if
        ///     there is a dependency cycle or an unresolved manager.
        /// </summary>
        public IReadOnlyList<int> InitializationOrder { get; }

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
            builder.Append($"Scope '{(Definition != null ? Definition.name : "?")}': {Issues.Count} issue(s)");
            foreach (ScopeIssue issue in Issues)
            {
                builder.Append($"\n  [{issue.Severity}] {issue.Message}");
            }

            return builder.ToString();
        }
    }
}
