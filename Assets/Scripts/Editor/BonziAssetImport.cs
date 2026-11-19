#if UNITY_EDITOR
using UnityEditor;
namespace RhythmWare.EditorTools
{
    public sealed class BonziAssetImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            bool extended=assetPath=="Assets/Resources/Boss/BonziExtendedAtlas.png";
            if(assetPath!="Assets/Resources/Boss/BonziAtlas.png"&&!extended)return;
            var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;t.mipmapEnabled=false;t.isReadable=false;
            t.filterMode=extended?UnityEngine.FilterMode.Bilinear:UnityEngine.FilterMode.Point;t.wrapMode=UnityEngine.TextureWrapMode.Clamp;t.npotScale=TextureImporterNPOTScale.None;
            t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=extended?4096:2048;
        }
    }
}
#endif
