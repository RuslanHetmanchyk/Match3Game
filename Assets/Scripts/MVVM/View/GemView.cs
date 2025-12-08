using DG.Tweening;
using MVVM.Model;
using MVVM.ViewModel;
using UnityEngine;

namespace MVVM.View
{
    public class GemView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Sequence currentSeq;

        public GemViewModel ViewModel { get; private set; }

        public void Init(GemModel model)
        {
            
        }

        public void Bind(GemViewModel vm, Sprite sprite)
        {
            Unbind();
            ViewModel = vm;
            spriteRenderer.sprite = sprite;
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
            currentSeq?.Kill();
            currentSeq = DOTween.Sequence();
            currentSeq.Append(transform.DOMove(worldTarget, GameConst.GemSwapSec).SetEase(Ease.OutCubic));
        }

        private void HandleDestroyed(GemViewModel vm)
        {
            currentSeq?.Kill();
            var seq = DOTween.Sequence();
            seq.Append(transform.DOScale(Vector3.zero, 0.25f));
            seq.Join(spriteRenderer.DOFade(0f, 0.25f));
            seq.OnComplete(() =>
            {
                gameObject.SetActive(false);
            });
        }

        private void OnDisable()
        {
            spriteRenderer.color = new Color(1,1,1,1);
            transform.localScale = Vector3.one;
            currentSeq?.Kill();
        }
    }
}
