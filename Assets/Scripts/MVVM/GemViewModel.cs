using System;
using Cysharp.Threading.Tasks;
using Match3.Model;
using UnityEngine;

namespace Match3.ViewModel
{
    public class GemViewModel
    {
        public GemModel Model { get; }
        public event Action<GemViewModel> OnDestroyed;
        public event Action<GemViewModel, Vector2> OnMoved; // world target pos

        public GemViewModel(GemModel model)
        {
            Model = model;
        }

        public async UniTask MoveTo(Vector2 worldTarget, float duration)
        {
            Model.IsMoving = true;
            OnMoved?.Invoke(this, worldTarget);
            await UniTask.DelayFrame(1); // view drives tween; we await very short time to keep flow
            await UniTask.Delay((int)(duration * 1000f));
            Model.IsMoving = false;
        }

        public void MarkDestroy()
        {
            Model.MarkedForDestroy = true;
            OnDestroyed?.Invoke(this);
        }
    }
}