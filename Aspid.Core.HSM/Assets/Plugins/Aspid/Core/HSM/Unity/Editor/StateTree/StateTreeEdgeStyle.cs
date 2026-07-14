// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// HSM graph edge drawing style: a Bezier curve, a straight line,
	/// or an orthogonal route with right-angle bends.
	/// </summary>
	public enum StateTreeEdgeStyle
	{
		Bezier,
		Straight,
		Orthogonal
	}
}
