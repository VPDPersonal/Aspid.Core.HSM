using _Scripts.Controllers;
using Aspid.Core.HSM;

namespace _Scripts
{
	[ParentState(typeof(RootState))]
	public class SinglePlayerState : IState
	{
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