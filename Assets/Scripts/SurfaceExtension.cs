using System.Collections.Generic;
using UnityEngine;

namespace AdaptiveRendering
{
    public static class SurfaceExtension
    {
        public const int s_SegmentBlockSize = 64;

        public static bool IsTileEmpty(Color[] colors, int x, int y, int count, int texWidth)
        {
            int emptyCount = 0;

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    if (colors[(y * count + i) * texWidth + x * count + j].r == 0) emptyCount++;
                }
            }

            return emptyCount * 100.0f / count / count > 99;
        }

        public static int CountActiveTiles(this Texture2D source, int blockSize = s_SegmentBlockSize)
        {
            var colors = source.GetPixels();

            int tileNums = 0;

            for (int i = 0; i < source.height / blockSize; i++)
            {
                for (int j = 0; j < source.width / blockSize; j++)
                {
                    if (IsTileEmpty(colors, j, i, blockSize, source.width))
                        continue;

                    tileNums++;
                }
            }

            return tileNums;
        }

        public static KeyValuePair<Texture2D, int[]> PackAtlas(this Texture2D source)
        {
            var blockSize = s_SegmentBlockSize;
            var blockNum = source.width / blockSize;

            var indices = new int[blockNum * blockNum];
            var colors = source.GetPixels();

            int tileIndex = 0;

            for (int i = 0; i < source.height / blockSize; i++)
            {
                for (int j = 0; j < source.width / blockSize; j++)
                {
                    bool isSkip = IsTileEmpty(colors, j, i, blockSize, source.width);
                    indices[j + i * blockNum] = isSkip ? -1 : tileIndex;
                    if (isSkip == false) tileIndex++;
                }
            }

            var atlasC = Mathf.ClosestPowerOfTwo(Mathf.CeilToInt(Mathf.Sqrt(tileIndex)) * blockSize) / blockSize;
            var mainTexture = new Texture2D(blockSize * atlasC, blockSize * atlasC, source.format, false, true);

            for (int i = 0; i < source.height / blockSize; i++)
            {
                for (int j = 0; j < source.width / blockSize; j++)
                {
                    if (indices[j + i * blockNum] == -1) continue;
                    int x = indices[j + i * blockNum] % atlasC;
                    int y = indices[j + i * blockNum] / atlasC;

                    Graphics.CopyTexture(source, 0, 0, j * blockSize, i * blockSize, blockSize, blockSize, mainTexture, 0, 0, x * blockSize, y * blockSize);
                }
            }

            return new KeyValuePair<Texture2D, int[]>(mainTexture, indices);
        }

        public static Texture2D UnpackAtlas(this Texture2D mainTexture, int[] indics, int resolution)
        {
            var blockSize = s_SegmentBlockSize;
            var blockNum = resolution / blockSize;

            var atlasC = mainTexture.width / blockSize;
            var sourceTex = new Texture2D(resolution, resolution, mainTexture.format, false, true);

            for (int i = 0; i < resolution / blockSize; i++)
            {
                for (int j = 0; j < resolution / blockSize; j++)
                {
                    if (indics[j + i * blockNum] == -1)
                        continue;

                    int x = indics[j + i * blockNum] % atlasC;
                    int y = indics[j + i * blockNum] / atlasC;

                    Graphics.CopyTexture(mainTexture, 0, 0, x * blockSize, y * blockSize, blockSize, blockSize, sourceTex, 0, 0, j * blockSize, i * blockSize);
                }
            }

            return sourceTex;
        }
    }
}