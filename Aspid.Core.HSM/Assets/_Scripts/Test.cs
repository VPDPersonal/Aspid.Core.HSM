using System;
using Aspid.Core.HSM;
using UnityEngine;

namespace _Scripts
{
	public class StateFactory : StateFactory<RootState>
	{
		protected override IState CreateStateInternal(Type type)
		{
			if(type == typeof(RootState))
			{
				return new RootState();
			}
			else if (type == typeof(SinglePlayerState))
			{
				return new SinglePlayerState();
			}
			return null;
		}
	}
	
	public class Test : MonoStateMachine
	{
		private void Awake()
		{
			var stateFactory = new StateFactory();
			Initialize(stateFactory);
		}

		protected override void OnUpdating()
		{
			base.OnUpdating();

			if (Input.GetKeyDown(KeyCode.Z))
			{
				ChangeState<RootState>();
			}
			
			if (Input.GetKeyDown(KeyCode.X))
			{
				ChangeState<RootState>();
			}
		}
	}
}