using _Scripts.Controllers;
using Aspid.Core.HSM;

namespace _Scripts
{
	[ControllerGroup]
	[ParentState(typeof(RootState))]
	public partial class SinglePlayerState : IState
	{
		public SinglePlayerState()
		{
			AddControllers(
				new TestAsyncController(),
				new TestController(nameof(SinglePlayerState)));
		}

		public void Enter()
		{
			var testController = new TestController(stateName: nameof(SinglePlayerState));
		}

		public void Exit()
		{
			throw new System.NotImplementedException();
		}
	}
}