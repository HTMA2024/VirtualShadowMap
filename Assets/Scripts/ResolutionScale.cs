namespace AdaptiveRendering
{
	public enum ResolutionScale
	{
		One,
        Half,
        Quarter,
        Eighth,
	}

	public static class ResolutionScaleExtensions
	{
		public static float AsMultiplier(this ResolutionScale mode) => mode switch
		{
			ResolutionScale.Eighth => 0.125f,
			ResolutionScale.Quarter => 0.25f,
			ResolutionScale.Half => 0.5f,
			_ => 1f
		};
	}
}
