using Match3.View;
using UnityEngine;
using Zenject;

namespace Match3.Installers
{
    public class GameInstaller : MonoInstaller
    {
        [SerializeField] private GemView gemPrefab;
        [SerializeField] private Transform gemParent;

        public override void InstallBindings()
        {
            Container.BindInstance(gemPrefab).AsSingle();
            Container.BindInstance(gemParent).AsSingle();
            // можно зарегистрировать дополнительные сервисы здесь
        }
    }
}