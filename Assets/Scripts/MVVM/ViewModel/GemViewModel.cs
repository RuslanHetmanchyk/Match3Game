using System;
using Cysharp.Threading.Tasks;
using MVVM.Model;
using UnityEngine;

namespace MVVM.ViewModel
{
    public class GemViewModel
    {
        public GemModel Model { get; }
        public event Action<GemViewModel> OnDestroyed;
        public event Action<GemViewModel, Vector2> OnMoved;

        public GemViewModel(GemModel model)
        {
            Model = model;
        }

        public async UniTask MoveTo(Vector2 worldTarget, float duration)
        {
            Model.IsMoving = true;
            OnMoved?.Invoke(this, worldTarget);
            await UniTask.DelayFrame(1);
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