using System.Collections.Generic;
using UnityEngine;

namespace Wynnyng.Presentation
{
    /// <summary>Native resources created at startup are owned by the generated world.</summary>
    public sealed class RuntimeAssets : MonoBehaviour
    {
        private readonly List<Object> owned = new List<Object>();
        public T Own<T>(T asset) where T : Object { owned.Add(asset); return asset; }
        public Material Material(Color color, bool vertexColors = false)
        {
            Shader shader = Resources.Load<Shader>("Shaders/Blockout");
            if (shader == null) throw new System.InvalidOperationException("Missing Resources/Shaders/Blockout.shader.");
            var material = Own(new Material(shader));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_UseVertexColors", vertexColors ? 1f : 0f);
            return material;
        }
        private void OnDestroy()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Destroy(owned[i]);
        }
    }
}
