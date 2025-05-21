using UnityEngine;

namespace DSTAnimParse
{

    public class DSTAnimElement : MonoBehaviour
    {
        public string layer;
        [HideInInspector]
        public uint layerHash;

        private SpriteRenderer spriteRenderer;
        private SpriteRenderer childSpriteRenderer;

        void Awake()
        {
            spriteRenderer = transform.GetComponent<SpriteRenderer>();
        }

        public void SetLayer(string layer)
        {
            this.layer = layer;
            layerHash = DSTAnimator.StrHash(layer);
        }

        /// <summary>
        /// 设置被替换的图片
        /// </summary>
        /// <param name="sprite"></param>
        public void SetOverrideSprite(Sprite sprite)
        {
            // 动画会接管sprite的控制，我需要创建一个子对象来设置图片
            if (childSpriteRenderer == null)
            {
                var child = new GameObject("OverrideSymbol");
                child.transform.SetParent(transform, false);
                childSpriteRenderer = child.AddComponent<SpriteRenderer>();
            }

            if (sprite != null)
            {
                spriteRenderer.enabled = false; //当前对象不显示图片，只让子节点显示
                childSpriteRenderer.sprite = sprite;
                childSpriteRenderer.gameObject.SetActive(true);
            }
            else
            {
                // if (childSpriteRenderer != null)
                // {
                //     Destroy(childSpriteRenderer.gameObject); 
                //     childSpriteRenderer = null;
                //     spriteRenderer.enabled = true;
                // }

                spriteRenderer.enabled = true;
                childSpriteRenderer.gameObject.SetActive(false);
            }

        }
    }
}