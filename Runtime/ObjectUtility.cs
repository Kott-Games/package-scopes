using UnityEngine;

namespace Kott.Scopes
{
    internal static class ObjectUtility
    {
        /// <summary>Destroys <paramref name="obj" /> in play mode and edit mode (tests).</summary>
        public static void DestroySafe(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }

        /// <summary>
        ///     Returns the active instance of a persistent host component, creating one on a new
        ///     GameObject if none exists. Hand-placed instances are found and reused.
        /// </summary>
        public static T FindOrCreateHost<T>(string name)
            where T : MonoBehaviour
        {
            if (!Application.isPlaying)
            {
                throw new System.InvalidOperationException($"{typeof(T).Name} is only available in play mode.");
            }

            T existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Exclude);
            if (existing != null)
            {
                return existing;
            }

            return new GameObject(name).AddComponent<T>();
        }
    }
}
