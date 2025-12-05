using Cysharp.Threading.Tasks;
using MVVM.View;
using UnityEngine;

namespace GameControllers
{
    public class InputController : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private BoardController board;

        private Vector2Int? selectedCell = null;
        private bool inputLocked = false;
        private float minSwipeDistance = 0.3f;

        private Vector2 startPos;
        private bool isTouching = false;

        private void Awake()
        {
            if (cam == null)
            {
                cam = Camera.main;
            }
        }

        private void Update()
        {
            if (inputLocked) return;

#if UNITY_EDITOR || UNITY_STANDALONE
            HandleMouse();
#else
            HandleTouch();
#endif
        }

        // ==============================
        // Mouse (Editor / PC)
        // ==============================
        private void HandleMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                isTouching = true;
                startPos = Input.mousePosition;
                selectedCell = TryGetCellFromScreen(Input.mousePosition);
            }

            if (Input.GetMouseButtonUp(0) && isTouching)
            {
                isTouching = false;
                var endPos = (Vector2)Input.mousePosition;

                if (selectedCell.HasValue == false)
                {
                    return;
                }

                TrySwipe(startPos, endPos);
            }
        }

        // ==============================
        // Touch (Mobile)
        // ==============================
        private void HandleTouch()
        {
            if (Input.touchCount == 0)
            {
                return;
            }

            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                isTouching = true;
                startPos = t.position;
                selectedCell = TryGetCellFromScreen(t.position);
            }

            if (t.phase == TouchPhase.Ended)
            {
                isTouching = false;
                var endPos = t.position;

                if (selectedCell.HasValue == false)
                {
                    return;
                }

                TrySwipe(startPos, endPos);
            }
        }

        private Vector2Int? TryGetCellFromScreen(Vector2 screenPos)
        {
            var world = cam.ScreenToWorldPoint(screenPos);
            var hit = Physics2D.Raycast(world, Vector2.zero);
            var gem = hit.collider?.GetComponent<GemView>();
            if (gem == null || gem.ViewModel == null)
            {
                return null;
            }

            return gem.ViewModel.Model.Position;
        }

        private void TrySwipe(Vector2 start, Vector2 end)
        {
            var delta = end - start;
            if (delta.magnitude < minSwipeDistance * Screen.dpi / 160f)
            {
                return;
            }

            Vector2Int dir;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                dir = delta.x > 0 ? Vector2Int.right : Vector2Int.left;
            }
            else
            {
                dir = delta.y > 0 ? Vector2Int.up : Vector2Int.down;
            }

            var from = selectedCell.Value;
            var to = from + dir;

            PerformSwap(from, to).Forget();
        }

        private async UniTaskVoid PerformSwap(Vector2Int a, Vector2Int b)
        {
            inputLocked = true;

            bool success = await board.SwapAndResolve(a, b);

            await UniTask.Delay(100);

            inputLocked = false;
        }
    }
}