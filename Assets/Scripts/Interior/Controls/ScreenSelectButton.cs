using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Кнопка выбора изображения на стекле. Общий ход и подпись — в AnimatedButtonControl.
    /// </summary>
    [ExecuteAlways]
    public sealed class ScreenSelectButton : AnimatedButtonControl
    {
        [Tooltip("Стекло кабины, на котором сменится картинка.")]
        public Renderer screen;
        [Tooltip("Какой прибор показать на этом стекле.")]
        public ScreenContent content = ScreenContent.Docking;

        public override string Prompt => label;
        public override bool Available => ScreenRouter.CanShow(screen, content);
        public override bool Wired => screen != null && content != ScreenContent.None;

        protected override bool PerformAction() => ScreenRouter.Show(screen, content);
    }
}
