using MVVM.View;
using Pools;
using UnityEngine;
using Zenject;

namespace Installers
{
    public class GemViewPoolInstaller : MonoInstaller
    {
        [SerializeField] private GemView prefabGemView;

        public override void InstallBindings()
        {
            Container.BindMemoryPool<GemView, GemViewPool>()
                .WithInitialSize(GameConst.BoardWidth * GameConst.BoardHeight)
                .FromComponentInNewPrefab(prefabGemView)
                .UnderTransformGroup("Pool(GemView)");
        }
    }
}