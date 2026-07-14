// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Layout mode for the HSM state tree on the canvas: root at the top growing down,
	/// root on the left growing right, or a radial view with the root in the center
	/// and tiers as rings.
	/// </summary>
	public enum StateTreeLayoutDirection
	{
		TopDown,
		LeftToRight,
		Radial
	}
}
