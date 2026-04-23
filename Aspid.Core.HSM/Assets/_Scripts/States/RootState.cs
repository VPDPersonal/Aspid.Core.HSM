using _Scripts.Controllers;
using Aspid.Core.HSM;

namespace _Scripts
{
	[ParentState(null)]
	public class RootState : IState
	{
		public void Enter()
		{
			var testController = new TestController(stateName: nameof(RootState));
		}

		public void Exit()
		{
			throw new System.NotImplementedException();
		}
	}
}