using DG.Tweening;
using Match3.ViewModel;
using MVVM;
using UnityEngine;

namespace Match3.View
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class GemView : MonoBehaviour
    {
        public GemViewModel ViewModel { get; private set; }
        private SpriteRenderer sr;
        private Sequence currentSeq;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        public void Bind(GemViewModel vm, Sprite sprite)
        {
            Unbind();
            ViewModel = vm;
            sr.sprite = sprite;
            vm.OnMoved += HandleMove;
            vm.OnDestroyed += HandleDestroyed;
        }

        private void Unbind()
        {
            if (ViewModel != null)
            {
                ViewModel.OnMoved -= HandleMove;
                ViewModel.OnDestroyed -= HandleDestroyed;
            }
            ViewModel = null;
        }

        private void HandleMove(GemViewModel vm, Vector2 worldTarget)
        {
            // stop any current animation
            currentSeq?.Kill();
            currentSeq = DOTween.Sequence();
            currentSeq.Append(transform.DOMove(worldTarget, GameConst.GemSwapSec).SetEase(Ease.OutCubic));
        }

        private void HandleDestroyed(GemViewModel vm)
        {
            currentSeq?.Kill();
            // simple scale + fade then destroy (return to pool should be handled by pool)
            var seq = DOTween.Sequence();
            seq.Append(transform.DOScale(Vector3.zero, 0.25f));
            seq.Join(sr.DOFade(0f, 0.25f));
            seq.OnComplete(() =>
            {
                // notify pool/controller via GameObject.SetActive(false) or callback
                gameObject.SetActive(false);
            });
        }

        private void OnDisable()
        {
            // reset visual state for pool reuse
            sr.color = new Color(1,1,1,1);
            transform.localScale = Vector3.one;
            currentSeq?.Kill();
        }
    }
}
