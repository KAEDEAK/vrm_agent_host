using System;
using System.Collections.Generic;
using UnityEngine;

public class MainThreadInvoker : MonoBehaviour {
    // メインスレッドで実行すべきアクションのスレッドセーフなキュー
    private static readonly Queue<Action> executionQueue = new Queue<Action>();

    /// <summary>
    /// 毎フレーム呼び出され、キュー内のアクションを1つだけ順次実行します。
    /// ★UPDATE★ 1フレームにつき最大1アクション。大量キュー時のFPS低下を防止。
    /// </summary>
    private void Update() {
        Action action = null;
        // キュー操作はロックして保護
        lock (executionQueue) {
            if (executionQueue.Count > 0) {
                action = executionQueue.Dequeue();
            }
        }
        // アクションがあれば実行
        if (action != null) {
            try {
                action.Invoke();
            }
            catch (Exception ex) {
                Debug.LogError($"Error in MainThreadInvoker: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// 他スレッドからメインスレッドで実行したいアクションを登録します。
    /// </summary>
    public static void Invoke(Action action) {
        if (action == null) {
            throw new ArgumentNullException(nameof(action));
        }
        lock (executionQueue) {
            executionQueue.Enqueue(action);
        }
    }
}
