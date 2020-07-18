using System;

namespace UnityAsync
{
	public struct WaitUntil : IAwaitInstruction
	{
		readonly Func<bool> condition;

		bool IAwaitInstruction.IsCompleted() => condition();

		/// <summary>
		/// Waits until the condition returns true before continuing.
		/// </summary>
		public WaitUntil(Func<bool> condition)
		{
			#if UNITY_EDITOR
			if(condition == null)
				throw new ArgumentNullException(nameof(condition), "This check only occurs in edit mode.");
			#endif
			
			this.condition = condition;
		}
	}
}