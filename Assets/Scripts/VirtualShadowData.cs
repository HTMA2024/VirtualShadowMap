using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AdaptiveRendering
{
    public sealed class VirtualShadowData : ScriptableObject
    {
        /// <summary>
        /// ������������.
        /// </summary>
        public Vector3 regionCenter = Vector3.zero;

        /// <summary>
        /// ���������С.
        /// </summary>
        public int regionSize = 1024;

        /// <summary>
        /// ҳ��ߴ�.
        /// </summary>
        public int pageSize = 16;

        /// <summary>
        /// ���mipmap�ȼ�
        /// </summary>
        public int maxMipLevel = 4;

        /// <summary>
        /// ����Tile�ĳߴ�.
        /// </summary>
        public TileResolution maxResolution = TileResolution._1024;

        /// <summary>
        /// �����Χ��.
        /// </summary>
        [SerializeField]
        public Bounds bounds;

        /// <summary>
        /// ��Դ����.
        /// </summary>
        [SerializeField]
        public Vector3 direction;

        /// <summary>
        /// ��Դ�任����.
        /// </summary>
        [SerializeField]
        public Matrix4x4 worldToLocalMatrix;

        /// <summary>
        /// ��Դ�任����.
        /// </summary>
        [SerializeField]
        public Matrix4x4 localToWorldMatrix;

        /// <summary>
        /// ��ӰͶӰ�����б�.
        /// </summary>
        [SerializeField]
        public PersistentMap<TileRequestData, Matrix4x4> lightProjections = new PersistentMap<TileRequestData, Matrix4x4>();

        /// <summary>
        /// ������Դ�б�.
        /// </summary>
        [SerializeField]
        public PersistentMap<TileRequestData, string> texAssets = new PersistentMap<TileRequestData, string>();

        /// <summary>
        /// ��Դ����.
        /// </summary>
        public int textureCount { get => texAssets.Count; }

        /// <summary>
        /// ҳ���Ӧ����������.
        /// </summary>
        public Rect regionRange
        {
            get
            {
                return new Rect(-regionSize / 2, -regionSize / 2, regionSize, regionSize);
            }
        }

        /// <summary>
        /// ���������Դ
        /// </summary>
        public void SetTexAsset(TileRequestData request, string key)
        {
            texAssets.Add(request, key);
        }

        /// <summary>
        /// ����������Դ
        /// </summary>
        public string GetTexAsset(TileRequestData request)
        {
            if (texAssets.TryGetValue(request, out var value))
                return value;

            return null;
        }

        /// <summary>
        /// ����������Դ
        /// </summary>
        public string GetTexAsset(int x, int y, int mipLevel)
        {
            return GetTexAsset(new TileRequestData(x, y, mipLevel));
        }

        /// <summary>
        /// ���������Ӧ��ͶӰ����
        /// </summary>
        public void SetMatrix(TileRequestData request, Matrix4x4 matrix)
        {
            lightProjections.Add(request, matrix);
        }

        /// <summary>
        /// ��ȡ�����Ӧ��ͶӰ����
        /// </summary>
        public Matrix4x4 GetMatrix(TileRequestData request)
        {
            if (lightProjections.TryGetValue(request, out var value))
                return value;

            return Matrix4x4.identity;
        }

#if UNITY_EDITOR
        public void SetupTextureImporter()
        {
            foreach (var it in this.texAssets)
            {
                var textureImporter = TextureImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(it.Value)) as TextureImporter;
                if (textureImporter != null)
                {
                    textureImporter.textureType = TextureImporterType.SingleChannel;
                    textureImporter.textureShape = TextureImporterShape.Texture2D;
                    textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    textureImporter.alphaSource = TextureImporterAlphaSource.None;
                    textureImporter.sRGBTexture = false;
                    textureImporter.ignorePngGamma = true;
                    textureImporter.mipmapEnabled = false;
                    textureImporter.filterMode = FilterMode.Point;
                    textureImporter.wrapMode = TextureWrapMode.Clamp;

                    var defaultSettings = textureImporter.GetDefaultPlatformTextureSettings();
                    defaultSettings.format = TextureImporterFormat.R16;
                    defaultSettings.textureCompression = TextureImporterCompression.Uncompressed;

                    textureImporter.SetPlatformTextureSettings(defaultSettings);

                    var standaloneSettings = textureImporter.GetPlatformTextureSettings("Standalone");
                    standaloneSettings.overridden = true;
                    standaloneSettings.format = TextureImporterFormat.R16;
                    standaloneSettings.textureCompression = TextureImporterCompression.Uncompressed;

                    textureImporter.SetPlatformTextureSettings(standaloneSettings);

                    var androidSettings = textureImporter.GetPlatformTextureSettings("Android");
                    androidSettings.overridden = true;
                    androidSettings.format = TextureImporterFormat.R16;
                    androidSettings.textureCompression = TextureImporterCompression.Uncompressed;

                    textureImporter.SetPlatformTextureSettings(androidSettings);

                    textureImporter.SaveAndReimport();
                }
            }
        }

        public void SaveAs(string path, string name = "VirtualShadowData.asset")
        {
            AssetDatabase.CreateAsset(this, Path.Join(path, name));
            AssetDatabase.SaveAssets();
        }
#endif
    }
}