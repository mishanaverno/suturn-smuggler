using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Орган, который держат рукой: пока кнопка нажата, он идёт за курсором. Отдельно от
    /// нажатия и от колеса, потому что это не событие, а продолжающееся действие — у него
    /// есть начало, ход и конец, и всё это время орган принадлежит руке, а не тому, на что
    /// сейчас наведён курсор.
    /// </summary>
    public interface IDraggable
    {
        void Grab(Ray ray);
        void Drag(Ray ray);
        void Drop();
    }

    /// <summary>
    /// Рычаг тяги: рукоятка, ходящая по направляющей между двумя упорами — «закрыто»
    /// и «полная».
    ///
    /// От крутилки отличается тем, что у него есть положение. Крутилка отдаёт щелчки, потому
    /// что у Δv нет «правильного» положения ручки; у тяги оно есть — рычаг стоит там, куда
    /// его поставили, и сколько сейчас тяги, видно по руке, не читая табло. Ради этого он и
    /// нужен, поэтому и берётся он рукой, а не колесом: колесо отдаёт счёт, а здесь двигают
    /// саму рукоятку.
    ///
    /// Поэтому же рычаг двигает свой трансформ сам, а не через ControlResponse: отклик —
    /// это то, что орган делает, сработав, а тут движение и есть величина.
    ///
    /// И поэтому рычаг — единственный орган, который сам отдаёт показание: его положение есть
    /// физический факт, спрашивать о нём устройство некого. Разъём у него показания
    /// (ReadingPort), а не ручки: величина абсолютная, а не щелчок.
    /// </summary>
    public class PanelLever : MonoBehaviour, IInteractable, IDraggable
    {
        [Tooltip("Куда отдаётся положение, 0…1. Ассет показания из папки разъёмов.")]
        public ReadingPort port;

        [Tooltip("Направление хода в местных осях объекта. Нормализуется.")]
        public Vector3 axis = Vector3.forward;

        [Tooltip("Длина хода от упора до упора, м.")]
        public float travel = 0.12f;

        [Range(0f, 1f), Tooltip("Где стоит рычаг при запуске.")]
        public float start;

        float value;
        float grabOffset;
        Vector3 home;

        /// <summary>Положение рычага: 0 — нижний упор, 1 — верхний.</summary>
        public double Value => value;

        void Awake() => home = transform.localPosition;

        void OnEnable()
        {
            value = Mathf.Clamp01(start);
            Apply();
            if (port != null && port.Assigned) ControlBus.Bind(port.id, () => Value);
        }

        void OnDisable()
        {
            if (port != null && port.Assigned) ControlBus.Unbind(port.id);
        }

        public string Prompt => port != null ? port.Title : name;

        public bool Available => port != null && port.Assigned;

        /// <summary>Рычаг не нажимают: щелчок по нему ничего не значит, его тянут.</summary>
        public void Interact() { }

        /// <summary>
        /// Рукоятка не прыгает под курсор: запоминается разница между ними, и дальше рычаг
        /// идёт за рукой, сохраняя её. Иначе неточное нажатие само по себе меняло бы тягу.
        /// </summary>
        public void Grab(Ray ray)
        {
            if (Pointer(ray, out float along)) grabOffset = value * travel - along;
        }

        public void Drag(Ray ray)
        {
            if (!Pointer(ray, out float along)) return;
            value = Mathf.Clamp01((along + grabOffset) / travel);
            Apply();
        }

        public void Drop() { }

        /// <summary>
        /// Куда показывает курсор, в метрах вдоль направляющей от нижнего упора. Берётся
        /// ближайшая к лучу точка направляющей: рычаг ходит по прямой, и рука может вести
        /// его только вдоль неё — поперечная часть движения мыши к делу не относится.
        ///
        /// Луч вдоль самой направляющей ближайшей точки не задаёт — тогда рычаг просто стоит:
        /// направления, в которое его двигают, в этот момент не существует.
        /// </summary>
        bool Pointer(Ray ray, out float along)
        {
            along = 0f;
            Vector3 dir = Direction;
            float slant = Vector3.Dot(dir, ray.direction);
            float denominator = 1f - slant * slant;
            if (denominator < 1e-4f) return false;

            Vector3 offset = Zero - ray.origin;
            along = (slant * Vector3.Dot(ray.direction, offset) - Vector3.Dot(dir, offset)) / denominator;
            return true;
        }

        /// <summary>Направляющая в мире. Поворот рычага не меняется, поэтому берётся с трансформа.</summary>
        Vector3 Direction => transform.rotation * axis.normalized;

        /// <summary>Нижний упор в мире.</summary>
        Vector3 Zero => transform.parent == null ? home : transform.parent.TransformPoint(home);

        void Apply() => transform.localPosition = home + transform.localRotation * axis.normalized * (value * travel);
    }
}
