using UnityEngine;

namespace DSTAnimParse
{
    /// <summary>
    /// 供状态机使用
    /// </summary>
    public class BaseStateBehaviour : StateMachineBehaviour
    {
        public delegate void NotityStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex);
        public delegate void NotityStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex);
        public delegate void NotifyStateMachineEnter(Animator animator, int stateMachinePathHash);
        public delegate void NotifyStateMachineExit(Animator animator, int stateMachinePathHash);

        public NotityStateEnter notityStateEnter;
        public NotityStateExit notityStateExit;
        public NotifyStateMachineEnter notifyStateMachineEnter;
        public NotifyStateMachineExit notifyStateMachineExit;

        override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            notityStateEnter?.Invoke(animator, stateInfo, layerIndex);
        }

        override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            notityStateExit?.Invoke(animator, stateInfo, layerIndex);
        }

        override public void OnStateMachineEnter(Animator animator, int stateMachinePathHash)
        {
            notifyStateMachineEnter?.Invoke(animator, stateMachinePathHash);
        }

        override public void OnStateMachineExit(Animator animator, int stateMachinePathHash)
        {
            notifyStateMachineExit?.Invoke(animator, stateMachinePathHash);
        }
    }

}