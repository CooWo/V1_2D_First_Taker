using UnityEngine;

namespace ShadowRPG.Runtime.Art
{
    /// <summary>이펙트·피격 플래시 재질 (Resources/Art/Materials). 빌드에 셰이더가 포함되도록 재질 에셋을 씁니다.</summary>
    public static class FxMaterials
    {
        static Material _additive, _flash;

        public static Material Additive => _additive != null ? _additive : _additive = Load("Art/Materials/Additive", "ShadowRPG/AdditiveSprite");
        public static Material Flash => _flash != null ? _flash : _flash = Load("Art/Materials/Flash", "ShadowRPG/SpriteFlash");

        static Material Load(string path, string shader)
        {
            var material = Resources.Load<Material>(path);
            if (material != null) return material;
            var found = Shader.Find(shader);
            return found != null ? new Material(found) : null;
        }
    }
}
