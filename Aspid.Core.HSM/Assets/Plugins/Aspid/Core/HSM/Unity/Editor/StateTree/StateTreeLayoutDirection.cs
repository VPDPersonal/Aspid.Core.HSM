// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Режим раскладки дерева состояний HSM на канвасе: корень сверху с ростом вниз,
	/// корень слева с ростом вправо или радиальный вид с корнем в центре
	/// и ярусами-кольцами.
	/// </summary>
	public enum StateTreeLayoutDirection
	{
		TopDown,
		LeftToRight,
		Radial
	}
}
