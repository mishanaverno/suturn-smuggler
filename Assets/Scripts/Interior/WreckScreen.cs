using Controls;
using Game;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Interior
{
    /// <summary>
    /// Экран гибели корабля: причина и путь назад — последнее сохранение, а без него игра
    /// с начала. Время стоит, ввод кабины глухой: погибшему кораблю управлять нечем.
    /// </summary>
    public class WreckScreen : MonoBehaviour
    {
        [Tooltip("Документ с разметкой WreckScreen.uxml.")]
        public UIDocument document;

        VisualElement root;
        Label reason;
        Button loadButton;
        bool shown;

        void Awake()
        {
            if (document != null) return;
            Debug.LogError($"WreckScreen на «{name}»: не указан UIDocument с разметкой экрана.", this);
            enabled = false;
        }

        // Разметку UIDocument собирает в своём OnEnable, порядок которого с нашим не задан.
        void Start()
        {
            VisualElement tree = document.rootVisualElement;
            root = tree.Q("root");
            reason = tree.Q<Label>("reason");
            loadButton = tree.Q<Button>("loadButton");
            loadButton.clicked += Restart;
            tree.Q<Button>("quitButton").clicked += PauseMenu.Quit;
        }

        void Update()
        {
            if (shown || root == null || SimMono.playerShip is not Ship { Wreck: string wreck }) return;
            shown = true;
            GameInput.Switch(GameInput.Context.Menu);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
            reason.text = wreck;
            loadButton.text = SaveGame.Exists ? "LOAD" : "RESTART";
            root.RemoveFromClassList("pause-menu--hidden");
        }

        static void Restart()
        {
            if (SaveGame.Exists)
            {
                SaveGame.Load();
                return;
            }
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
