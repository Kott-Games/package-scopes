using UnityEngine;

namespace Kott.Scopes.Demo
{
    /// <summary>Creates the demo's primitive visuals in code, so the scenes need no materials or meshes.</summary>
    public static class Primitives
    {
        private static readonly int _colorId = Shader.PropertyToID("_Color");
        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock _block;

        /// <summary>
        ///     Creates a collider-less primitive under <paramref name="parent" />. Primitives use the render
        ///     pipeline's default material, so this works in Built-in and URP.
        /// </summary>
        public static Renderer Create(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale,
            Color color)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            Object.Destroy(visual.GetComponent<Collider>());
            visual.name = "Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;

            Renderer renderer = visual.GetComponent<Renderer>();
            Tint(renderer, color);
            return renderer;
        }

        /// <summary>Colors <paramref name="renderer" /> without creating a material copy.</summary>
        public static void Tint(Renderer renderer, Color color)
        {
            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor(_colorId, color); // Built-in
            _block.SetColor(_baseColorId, color); // URP
            renderer.SetPropertyBlock(_block);
        }
    }
}
