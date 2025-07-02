using UnityEngine;

/// <summary>
/// アニメーション状態の開始・終了を監視するStateMachineBehaviour
/// </summary>
public class AnimationStateBehaviour : StateMachineBehaviour
{
    public static System.Action<string> OnAnimationEnter;
    public static System.Action<string> OnAnimationExit;

    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        string stateName = GetStateName(animator, stateInfo, layerIndex);
        Debug.Log($"[AnimationStateBehaviour] Animation entered: {stateName}");
        OnAnimationEnter?.Invoke(stateName);
    }

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        string stateName = GetStateName(animator, stateInfo, layerIndex);
        Debug.Log($"[AnimationStateBehaviour] Animation exited: {stateName}");
        OnAnimationExit?.Invoke(stateName);
    }

    private string GetStateName(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // ステート名を取得する方法
        var controller = animator.runtimeAnimatorController;
        if (controller != null)
        {
            foreach (var clip in controller.animationClips)
            {
                if (clip.name.GetHashCode() == stateInfo.shortNameHash ||
                    Animator.StringToHash(clip.name) == stateInfo.shortNameHash)
                {
                    return clip.name;
                }
            }
        }
        
        // フォールバック: ハッシュ値を文字列として返す
        return $"State_{stateInfo.shortNameHash}";
    }
}
