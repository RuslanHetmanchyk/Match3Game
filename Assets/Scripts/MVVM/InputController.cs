using UnityEngine;
using Cysharp.Threading.Tasks;
using Match3.Controllers;
using Match3.View;

namespace Match3.InputSystem
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
                cam = Camera.main;
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
                
                Debug.LogError(selectedCell);
            }

            if (Input.GetMouseButtonUp(0) && isTouching)
            {
                isTouching = false;
                var endPos = (Vector2)Input.mousePosition;

                if (selectedCell.HasValue == false)
                    return;

                TrySwipe(startPos, endPos);
            }
        }

        // ==============================
        // Touch (Mobile)
        // ==============================
        private void HandleTouch()
        {
            if (Input.touchCount == 0) return;

            Touch t = Input.GetTouch(0);

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
                    return;

                TrySwipe(startPos, endPos);
            }
        }

        // ==============================
        // Core Logic
        // ==============================

        private Vector2Int? TryGetCellFromScreen(Vector2 screenPos)
        {
            Vector2 world = cam.ScreenToWorldPoint(screenPos);
            RaycastHit2D hit = Physics2D.Raycast(world, Vector2.zero);

            if (hit.collider == null)
                return null;

            var gem = hit.collider.GetComponent<GemView>();
            if (gem == null || gem.ViewModel == null)
                return null;

            return gem.ViewModel.Model.Position;
        }

        private void TrySwipe(Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;

            if (delta.magnitude < minSwipeDistance * Screen.dpi / 160f)
            {
                // слишком короткое движение — считаем тапом, но не обрабатываем
                return;
            }

            // определить направление свайпа
            Vector2Int dir;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                dir = delta.x > 0 ? Vector2Int.right : Vector2Int.left;
            else
                dir = delta.y > 0 ? Vector2Int.up : Vector2Int.down;

            Vector2Int from = selectedCell.Value;
            Vector2Int to = from + dir;

            // передать в BoardController
            PerformSwap(from, to).Forget();
        }

        private async UniTaskVoid PerformSwap(Vector2Int a, Vector2Int b)
        {
            inputLocked = true;

            bool success = await board.SwapAndResolve(a, b);

            // если match был найден — board сам обработает цикл
            // если нет — swap будет отменён анимацией
            await UniTask.Delay(100); // защита от спама

            inputLocked = false;
        }
    }
}