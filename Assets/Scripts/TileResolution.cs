namespace AdaptiveRendering
{
    public enum TileResolution
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096
    }

    public static class TileResolutionExtensions
    {
        public static int AsPixelCount(this TileResolution mode) => mode switch
        {
            TileResolution._256 => 256,
            TileResolution._512 => 512,
            TileResolution._1024 => 1024,
            TileResolution._2048 => 2048,
            TileResolution._4096 => 4096,
            _ => 1
        };
    }
}
