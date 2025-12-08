using MVVM.Model;
using MVVM.View;
using UnityEngine;
using Zenject;

namespace Pools
{
    public class GemViewPool : MonoMemoryPool<GemModel, GemView>
    {
        protected override void Reinitialize(GemModel model, GemView view)
        {
            view.Init(model);
        }
    }
}