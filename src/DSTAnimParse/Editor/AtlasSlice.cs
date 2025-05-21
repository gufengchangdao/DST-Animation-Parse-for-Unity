using UnityEditor;
using UnityEngine;
using UnityEditor.U2D.Sprites;
using System.Collections.Generic;

namespace DSTAnimParse
{
    public class AtlasSlice
    {
        public static void SliceAtlas(BuildData build, AtlasData atlas, int index)
        {
            string relativePath = atlas.savePath.Replace(Application.dataPath, "Assets").Replace('\\', '/'); //以Assets开头
            TextureImporter importer = AssetImporter.GetAtPath(relativePath) as TextureImporter;

            if (importer == null)
            {
                Debug.LogError("无法获取纹理导入器: " + atlas.savePath);
                return;
            }

            // 设置为Sprite(2D and UI)类型
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 1; //这个不要缩放

            // 创建精灵数据提供者
            var dataProvider = new SpriteDataProviderFactories().GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            // 创建切片列表
            var spriteRects = new List<SpriteRect>();

            // 填充切片数据
            foreach (var symbol in build.symbols.Values)
            {
                foreach (var frame in symbol.frames)
                {
                    if (frame.atlas_depth != index) continue;

                    var mipmap = atlas.Mipmaps[0];

                    var spriteRect = new SpriteRect
                    {
                        name = $"{symbol.name}_{frame.framenum}",
                        rect = new Rect(
                            frame.atlas_bbox.x * mipmap.width,
                            frame.atlas_bbox.y * mipmap.height,
                            frame.atlas_bbox.width * mipmap.width,
                            frame.atlas_bbox.height * mipmap.height
                        ),
                        alignment = SpriteAlignment.Custom,
                        pivot = frame.GetPivot()
                    };
                    spriteRects.Add(spriteRect);
                }
            }

            // 应用切片设置
            dataProvider.SetSpriteRects(spriteRects.ToArray());
            dataProvider.Apply();

            // 重新导入资源
            AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);

            // Debug.Log($"{build.atlasNames[index]}图集切片完成，总计{spriteRects.Count}个切片");
        }
    }

}
