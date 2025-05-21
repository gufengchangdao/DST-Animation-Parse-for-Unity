using System.Collections.Generic;
using UnityEngine;

namespace DSTAnimParse
{
    /// <summary>
    /// 用于饥荒动画预制件的动画播放
    /// 没考虑层级，全都默认层级0
    /// </summary>
    [SelectionBase]
    public class DSTAnimator : MonoBehaviour
    {
        /// <summary>
        /// 动画播放结束
        /// </summary>
        /// <param name="animName">动画名</param>
        /// <param name="isSafeGuard">是否触发保底回调</param>
        public delegate void OnPlayEventCallBack(string animName, bool isSafeGuard);
        public event OnPlayEventCallBack OnPlayEndCallBack;

        /// <summary>
        /// <symbol, <切片索引, 切片>>
        /// 用于查找图片的字典，用于SWAP_XXX一类的替换
        /// </summary>
        public Dictionary<string, Dictionary<int, Sprite>> spriteOverrideDict = new();

        /// <summary>
        /// 当前播放的动画数据
        /// </summary>
        private readonly AnimData data = new();
        private readonly Dictionary<string, AnimationClip> animClipDic = new();
        private readonly Dictionary<string, DSTAnimElement> elements = new();
        /// <summary>
        /// layer管理，表示哪些layer需要隐藏
        /// </summary>
        private readonly HashSet<string> HideLayerSet = new();
        private Animator animator;

        void Awake()
        {
            animator = GetComponent<Animator>();

            if (animator.runtimeAnimatorController != null)
            {
                AnimationClip[] animationClips = animator.runtimeAnimatorController.animationClips;
                foreach (var clip in animationClips)
                    animClipDic[clip.name] = clip;
            }

            foreach (Transform t in transform)
            {
                if (t.TryGetComponent(out DSTAnimElement element))
                    elements.Add(t.name, element);
            }
        }

        /// <summary>
        /// 播放动画
        /// </summary>
        /// <param name="animName">要播放的动画名</param>
        /// <param name="time">动画开始时间，小于0从当前时间开始播放，大于0时范围为[0,1]，表示动画从什么位置开始播放</param>
        public void Play(string animName, float time = -1)
        {
            var animLen = 1f;
            if (animClipDic.TryGetValue(animName, out var clip))
            {
                animLen = clip.length;
                data.isLoop = clip.isLooping;
            }

            if (time < 0 && data.animName != animName)
                time = 0;
            if (time >= 0)
                data.remainingTime = (1 - Mathf.Clamp01(time)) * animLen;

            data.animName = animName;
            data.time = animLen;
            data.isPlayEnd = false;


            if (time >= 0)
                animator.Play(animName, 0, time);
            else
                animator.Play(animName, 0);

        }

        /// <summary>
        /// 隐藏指定层的所有元素
        /// </summary>
        /// <param name="layer"></param>
        public void Hide(string layer)
        {
            var layerHash = StrHash(layer);
            foreach (var element in elements.Values)
                if (element.layerHash == layerHash)
                    element.gameObject.SetActive(false);

            HideLayerSet.Add(layer);
        }

        // TODO 可以用一个字典来缓存，根据调用次数来保存数据，避免每次都字符串解析
        public void SetAnimLayer(string layerStr)
        {
            string[] parts = layerStr.Split('|');
            for (int i = 0; i < parts.Length; i += 2)
            {
                var layer = parts[i + 1];
                if (elements.TryGetValue(parts[i], out var element))
                {
                    element.SetLayer(layer);
                    if (HideLayerSet.Contains(layer))
                        element.gameObject.SetActive(false);
                    else
                        element.gameObject.SetActive(true);
                }
            }
        }

        public void SetAnimSprite(string spriteStr)
        {
            string[] parts = spriteStr.Split('|');

            for (int i = 0; i < parts.Length; i += 2)
            {
                var childPath = parts[i];
                var spriteName = parts[i + 1];

                if (elements.TryGetValue(childPath, out var element))
                {
                    int index = spriteName.LastIndexOf('_');
                    string symbol = index >= 0 ? spriteName[..index] : spriteName;

                    if (spriteOverrideDict.TryGetValue(symbol, out var sprites))
                    {
                        if (!int.TryParse(spriteName[(index + 1)..], out var number))
                        {
                            Debug.LogWarning($"无法解析精灵编号: {spriteName}");
                            continue;
                        }

                        if (sprites.TryGetValue(number, out var sprite))
                        {
                            // 有图片就替换
                            element.SetOverrideSprite(sprite);
                        }
                        else if (Mathf.Approximately(data.remainingTime, data.time) && data.time != 0 && sprites.TryGetValue(0, out sprite))
                        {
                            // 找不到就不替换，但如果是第一帧就尝试使用xxx_0图片
                            //TODO 如果没有0图是不是要再找1还是其他的，需要验证
                            element.SetOverrideSprite(sprite);
                        }
                    }

                }
            }
        }

        public void OverrideSymbol(string symbol, Dictionary<int, Sprite> sprites)
        {
            if (sprites.Count <= 0)
                Debug.LogWarning($"传入的替换字典为空");
            ClearOverrideSymbol(symbol);
            spriteOverrideDict[symbol] = sprites;
        }

        public void ClearOverrideSymbol(string symbol)
        {
            spriteOverrideDict.Remove(symbol);
        }

        public void Show(string layer)
        {
            var layerHash = StrHash(layer);
            foreach (var element in elements.Values)
                if (element.layerHash == layerHash)
                    element.gameObject.SetActive(true);

            HideLayerSet.Remove(layer);
        }

        public void OnAnimEndCallBack(string animName)
        {
            if (data.isPlayEnd)
                return;
            if (data.animName != animName)
                return;

            OnAnimEnd(false);
        }

        private void LateUpdate()
        {
            if (animator == null)
                return;

            if (data.animName == string.Empty)
                return;

            if (data.isPlayEnd)
                return;

            float t = animator.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            data.remainingTime -= t;

            //保底多隔一帧防止出现先保底再执行完毕的异常
            if (data.remainingTime < -t)
                OnAnimEnd(true);
        }

        private void OnAnimEnd(bool isSafeGuard)
        {
            data.isPlayEnd = true;

            var animName = data.animName;

            if (data.isLoop)
            {
                data.isPlayEnd = false;
                data.remainingTime = data.time;
            }
            else
            {
                data.Reset();
            }

            OnPlayEndCallBack?.Invoke(animName, isSafeGuard);
        }

        /// <summary>
        /// 科雷动画字符串转哈希的函数，估计用的都是这个计算
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static uint StrHash(string str)
        {
            int len = str.Length;
            uint h = 0;

            for (int i = 0; i < len; i++)
            {
                uint c = char.ToLower(str[i]);
                h = c + (h << 6) + (h << 16) - h;
            }

            return h;
        }

        public class AnimData
        {
            /// <summary>
            /// 动画名
            /// </summary>
            public string animName = string.Empty;
            /// <summary>
            /// 动画时长
            /// </summary>
            public float time = 0;
            /// <summary>
            /// 保底剩余时间
            /// </summary>
            public float remainingTime = 0;
            /// <summary>
            /// 播放结束
            /// </summary>
            public bool isPlayEnd = false;
            /// <summary>
            /// 是否循环
            /// </summary>
            public bool isLoop = false;
            public void Reset()
            {
                animName = string.Empty;
                time = 0;
                remainingTime = 0;
                isPlayEnd = false;
                isLoop = false;
            }
        }
    }
}