using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Shared palette for the HSM State Tree window in the spirit of Aspid.FastTools' dark theme:
	/// canvas, panel, and node colors, active-chain and selection highlighting,
	/// and accent colors for tree depth levels.
	/// </summary>
	public static class StateTreePalette
	{
		public static readonly Color canvasBackground = new(0.055f, 0.055f, 0.055f);
		public static readonly Color gridLine = new(0.09f, 0.09f, 0.09f);
		public static readonly Color gridMajorLine = new(0.125f, 0.125f, 0.125f);

		public static readonly Color panelBackground = new(0.118f, 0.118f, 0.118f);
		public static readonly Color panelBorder = new(0.176f, 0.176f, 0.176f);
		public static readonly Color cardBackground = new(0.157f, 0.157f, 0.157f);
		public static readonly Color cardBorder = new(0.216f, 0.216f, 0.216f);

		public static readonly Color textPrimary = new(0.85f, 0.85f, 0.85f);
		public static readonly Color textSecondary = new(0.61f, 0.61f, 0.61f);
		public static readonly Color textDim = new(0.45f, 0.45f, 0.45f);

		public static readonly Color nodeBackground = new(0.165f, 0.165f, 0.165f);
		public static readonly Color nodeBackgroundHover = new(0.204f, 0.204f, 0.204f);
		public static readonly Color nodeBorder = new(0.302f, 0.302f, 0.302f);
		public static readonly Color nodeBorderHover = new(0.45f, 0.45f, 0.45f);
		public static readonly Color nodeShadow = new(0f, 0f, 0f, 0.3f);

		/// <summary>Dark text used on top of light header plates (depth colors and active amber).</summary>
		public static readonly Color headerText = new(0.08f, 0.08f, 0.1f);

		public static readonly Color selectionBorder = new(0.267f, 0.529f, 0.878f);
		public static readonly Color activeBackground = new(0.333f, 0.216f, 0.039f);
		public static readonly Color activeBorder = new(0.961f, 0.725f, 0.333f);
		public static readonly Color activeText = new(1f, 0.922f, 0.686f);

		public static readonly Color searchHighlight = new(0.95f, 0.95f, 0.95f);

		public static readonly Color edge = new(0.27f, 0.27f, 0.27f);
		public static readonly Color activeEdge = new(0.961f, 0.725f, 0.333f);
		public static readonly Color activeEdgeRunner = new(1f, 0.922f, 0.686f);
		public static readonly Color transitionEdge = new(0.29f, 0.62f, 0.58f);
		public static readonly Color transitionEdgeFocused = new(0.42f, 0.82f, 0.76f);

		public static readonly Color chipControllersDot = new(0.353f, 0.635f, 0.937f);

		public static readonly Color minimapViewportFill = new(1f, 1f, 1f, 0.05f);
		public static readonly Color minimapViewportBorder = new(1f, 1f, 1f, 0.4f);

		public static readonly Color toolbarButtonHover = new(1f, 1f, 1f, 0.06f);
		public static readonly Color toggleOnBackground = new(0.267f, 0.529f, 0.878f, 0.2f);
		public static readonly Color toggleOnBorder = new(0.267f, 0.529f, 0.878f, 0.55f);

		private static readonly Color[] s_depthColors =
		{
			new(0.639f, 0.525f, 0.906f),
			new(0.353f, 0.635f, 0.937f),
			new(0.302f, 0.788f, 0.741f),
			new(0.949f, 0.671f, 0.353f),
			new(0.918f, 0.494f, 0.663f),
			new(0.757f, 0.827f, 0.427f)
		};

		/// <summary>
		/// Returns the accent color for a tree depth level (cycles through the palette).
		/// </summary>
		/// <param name="depth">Node depth, 0 is the root.</param>
		public static Color GetDepthColor(int depth) =>
			s_depthColors[Mathf.Abs(depth) % s_depthColors.Length];

		/// <summary>Returns the color with its alpha multiplied by <paramref name="factor"/>.</summary>
		public static Color Faded(Color color, float factor)
		{
			color.a *= factor;
			return color;
		}
	}
}
