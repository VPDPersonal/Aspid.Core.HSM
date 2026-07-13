// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Стиль отрисовки рёбер графа HSM: кривая Безье, прямая линия
	/// или ортогональный маршрут с изломами под прямым углом.
	/// </summary>
	public enum StateTreeEdgeStyle
	{
		Bezier,
		Straight,
		Orthogonal
	}
}
