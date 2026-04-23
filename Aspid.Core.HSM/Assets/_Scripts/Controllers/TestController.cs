using Aspid.Core.HSM;
using UnityEngine;

namespace _Scripts.Controllers
{
	public class TestController : IController, IUpdateController
	{
		private readonly string _stateName;

		public TestController(string stateName)
		{
			_stateName = stateName;
		}
		
		public void Update(float deltaTime)
		{
			Debug.Log(_stateName);
		}
	}
}