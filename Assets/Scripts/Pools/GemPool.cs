using System.Collections.Generic;
using MVVM.View;
using UnityEngine;

namespace Pools
{
    public class GemPool
    {
        private readonly GemView prefab;
        private readonly Transform parent;
        private readonly Stack<GemView> pool = new Stack<GemView>();

        public GemPool(GemView prefab, Transform parent, int initial = 50)
        {
            this.prefab = prefab;
            this.parent = parent;
            for (int i = 0; i < initial; i++)
            {
                var go = UnityEngine.Object.Instantiate(prefab, parent);
                go.gameObject.SetActive(false);
                pool.Push(go);
            }
        }

        public GemView Rent()
        {
            if (pool.Count > 0)
            {
                var g = pool.Pop();
                g.gameObject.SetActive(true);
                return g;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, parent);
            return instance;
        }

        public void Return(GemView view)
        {
            view.gameObject.SetActive(false);
            pool.Push(view);
        }
    }
}