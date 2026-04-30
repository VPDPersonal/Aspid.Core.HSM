using System.Threading;
using Aspid.Core.HSM;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Scripts.Controllers
{
	public class TestController : IUpdateController, IEnterController
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

		public void OnEnter()
		{
			throw new System.NotImplementedException();
		}
	}
}