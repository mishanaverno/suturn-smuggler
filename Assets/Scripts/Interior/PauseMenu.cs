using Controls;
using Game;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Interior
{
    /// <summary>
    /// Меню игры: пауза, сохранение, загрузка, выход. Открывается Esc только на ногах — в
    /// кресле Esc встаёт с места. Пока меню открыто, время стоит (timeScale 0), а кабина
    /// ввода не слышит.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        const string Hidden = "pause-menu--hidden";

        [Tooltip("Документ с разметкой PauseMenu.uxml.")]
        public UIDocument document;

        VisualElement root;
        Button loadButton;
        Label status;
        bool open;
        GameInput.Context resume;
        CursorLockMode resumeLock;
        bool resumeVisible;

        void Awake()
        {
            if (document != null) return;
            Debug.LogError($"PauseMenu на «{name}»: не указан UIDocument с разметкой меню.", this);
            enabled = false;
        }

        // Разметку UIDocument собирает в своём OnEnable, порядок которого с нашим не задан.
        void Start()
        {
            VisualElement tree = document.rootVisualElement;
            root = tree.Q("root");
            loadButton = tree.Q<Button>("loadButton");
            status = tree.Q<Label>("status");
            tree.Q<Button>("resumeButton").clicked += Close;
            tree.Q<Button>("saveButton").clicked += () =>
            {
                SaveGame.Save();
                status.text = "SAVED";
                loadButton.SetEnabled(true);
            };
            loadButton.clicked += SaveGame.Load;
            tree.Q<Button>("quitButton").clicked += Quit;
        }

        void Update()
        {
            if (root == null) return;
            if (!open && GameInput.PauseMenu.WasPressedThisFrame()) Open();
            else if (open && GameInput.MenuBack.WasPressedThisFrame()) Close();
        }

        void Open()
        {
            open = true;
            resume = GameInput.Current;
            resumeLock = Cursor.lockState;
            resumeVisible = Cursor.visible;
            GameInput.Switch(GameInput.Context.Menu);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
            status.text = "";
            loadButton.SetEnabled(SaveGame.Exists);
            root.RemoveFromClassList(Hidden);
        }

        void Close()
        {
            open = false;
            root.AddToClassList(Hidden);
            Time.timeScale = 1f;
            GameInput.Switch(resume);
            Cursor.lockState = resumeLock;
            Cursor.visible = resumeVisible;
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
