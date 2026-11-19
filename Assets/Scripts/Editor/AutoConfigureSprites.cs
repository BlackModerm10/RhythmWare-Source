using System.IO;
using UnityEditor;
using UnityEngine;

namespace RhythmWare.EditorTools
{
    public class AutoConfigureSprites : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (assetPath.Contains("Assets/Sprites"))
            {
                TextureImporter importer = (TextureImporter)assetImporter;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
            }
        }

        [MenuItem("RhythmWare/Convertir todas las imagenes en Assets-Sprites a Sprites 2D")]
        public static void ForceConvertAllSpritesInFolder()
        {
            string folderPath = "Assets/Sprites";
            if (!Directory.Exists(folderPath)) return;

            string[] filePaths = Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories);
            int converted = 0;

            foreach (string path in filePaths)
            {
                string relativePath = path.Replace("\\", "/");
                if (relativePath.StartsWith(Application.dataPath))
                {
                    relativePath = "Assets" + relativePath.Substring(Application.dataPath.Length);
                }

                TextureImporter importer = AssetImporter.GetAtPath(relativePath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single; // Must be Single, NOT Multiple!
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                    converted++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[RhythmWare] ¡Se forzó la conversión de {converted} imágenes en Assets/Sprites a Sprites 2D de modo Único (Single)!");
        }
    }
}
