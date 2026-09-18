using UnityEngine;

namespace Utilities
{
    /// <summary>Поворот картинки на стекле. Своё имя, а не UnityEngine.ScreenOrientation.</summary>
    public enum ScreenTurn { Deg0, Deg90, Deg180, Deg270 }

    /// <summary>
    /// Стекло прибора: то общее, что есть у любого экрана в кокпите — большого
    /// навигационного и малого текстового. Развёртка грани, пропорции текстуры и материал,
    /// которым текстура кладётся на поверхность.
    /// </summary>
    public static class ScreenGlass
    {
        // Экраны снимаются каждый своей камерой, и камера видит всё, что попало в её кадр на
        // слое экранов. Поэтому стенды стоят порознь на общей сетке мест, далеко от кабины и
        // друг от друга: иначе камера одного экрана сняла бы разметку соседнего. Шаг взят с
        // запасом на самый большой прибор — его кадр меряется пикселями текстуры.
        const float RigPitch = 4000f;
        const float RigDepth = -1000f;

        static int rigs;

        /// <summary>Свободное место для стенда экрана: камеры, холста и разметки.</summary>
        public static Vector3 NextRigPosition() => new(rigs++ * RigPitch, RigDepth, 0f);

        /// <summary>
        /// Экран — обычно не Quad, а грань, вырезанная из модели монитора. У такой грани UV
        /// достались от общей развёртки корпуса: островок в атласе, возможно повёрнутый или
        /// зеркальный. Текстура прибора легла бы по ним и показалась бы не так.
        ///
        /// Поэтому развёртка растягивается на всю текстуру. Это не вкусовое решение:
        /// на техническом экране картинка обязана занимать всё стекло целиком, иного
        /// правильного варианта нет, и выводится он из самой грани.
        ///
        /// Правится копия меша (MeshFilter.mesh, не sharedMesh): исходный ассет модели
        /// трогать нельзя, он общий для всех её копий.
        /// </summary>
        public static void NormalizeUV(Renderer surface, ScreenTurn turn, bool flipU, bool flipV)
        {
            if (surface == null) return;
            MeshFilter filter = surface.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;

            Vector2[] uv = filter.sharedMesh.uv;
            if (uv == null || uv.Length == 0)
            {
                Debug.LogWarning($"ScreenGlass: у меша экрана «{surface.name}» нет развёртки — " +
                    "текстуру не на что положить.", surface);
                return;
            }

            Vector2 min = uv[0], max = uv[0];
            foreach (Vector2 point in uv)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            Vector2 size = max - min;
            if (size.x <= 1e-6f || size.y <= 1e-6f)
            {
                Debug.LogWarning($"ScreenGlass: развёртка экрана «{surface.name}» вырождена " +
                    $"({size}) — растянуть её на текстуру нельзя.", surface);
                return;
            }

            for (int i = 0; i < uv.Length; i++)
            {
                Vector2 point = new((uv[i].x - min.x) / size.x, (uv[i].y - min.y) / size.y);
                point = Turn(point, turn);
                uv[i] = new Vector2(flipU ? 1f - point.x : point.x, flipV ? 1f - point.y : point.y);
            }
            filter.mesh.uv = uv;
        }

        /// <summary>
        /// Ширина текстуры подгоняется под пропорции стекла: если экран не 4:3, картинка
        /// на нём иначе растянулась бы, и круглая метка перестала бы быть круглой, а буквы
        /// разъехались бы вширь. Высота остаётся мерой разрешения и задаётся в инспекторе.
        ///
        /// Размеры берутся не из осей меша: у грани, вырезанной из модели, ось «вширь» может
        /// быть любой. Ширина и высота меряются вдоль самой развёртки — по рёбрам, идущим
        /// вдоль U и вдоль V. Так работает и Quad, и произвольная грань.
        /// </summary>
        public static int TextureWidth(Renderer surface, int textureHeight, int fallback)
        {
            if (surface == null) return fallback;
            MeshFilter filter = surface.GetComponent<MeshFilter>();
            Mesh mesh = filter == null ? null : filter.sharedMesh;
            if (mesh == null) return fallback;

            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;
            int[] triangles = mesh.triangles;
            if (uv == null || uv.Length != vertices.Length) return fallback;

            Vector3 lossy = surface.transform.lossyScale;
            float width = 0f, height = 0f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    int a = triangles[i + e], b = triangles[i + (e + 1) % 3];
                    Vector2 duv = uv[b] - uv[a];
                    Vector3 edge = Vector3.Scale(vertices[b] - vertices[a], lossy);
                    // Ребро вдоль U меряет ширину, вдоль V — высоту. Косые рёбра (диагонали
                    // грани) не меряют ничего и пропускаются.
                    if (Mathf.Abs(duv.x) > 0.01f && Mathf.Abs(duv.y) < 0.01f)
                    {
                        width = Mathf.Max(width, edge.magnitude / Mathf.Abs(duv.x));
                    }
                    else if (Mathf.Abs(duv.y) > 0.01f && Mathf.Abs(duv.x) < 0.01f)
                    {
                        height = Mathf.Max(height, edge.magnitude / Mathf.Abs(duv.y));
                    }
                }
            }

            if (width <= 0f || height <= 0f)
            {
                Debug.LogWarning($"ScreenGlass: по развёртке экрана «{surface.name}» (меш {mesh.name}) " +
                    $"не удалось померить стороны ({width:F3} × {height:F3}) — пропорции текстуры " +
                    "оставлены как есть. Развёртка грани должна идти вдоль U и V.", surface);
                return fallback;
            }
            int pixels = Mathf.Max(1, Mathf.RoundToInt(textureHeight * width / height));
            Debug.Log($"ScreenGlass: экран «{surface.name}», меш {mesh.name}, " +
                $"{width:F3} × {height:F3} м, текстура {pixels} × {textureHeight}.", surface);
            return pixels;
        }

        /// <summary>
        /// Текстура попадает на поверхность через собственный материал прибора: чужой был бы
        /// освещаемым, и экран темнел бы вместе с кабиной, хотя светится сам.
        /// </summary>
        public static void Show(Renderer surface, Texture texture)
        {
            if (surface == null) return;

            Shader shader = Resources.Load<Shader>("Shaders/ScreenSurface");
            if (shader == null)
            {
                Debug.LogWarning("ScreenGlass: нет шейдера Shaders/ScreenSurface, экран будет освещаемым.", surface);
                surface.material.mainTexture = texture;
                return;
            }
            surface.material = new Material(shader) { name = "Screen", mainTexture = texture };
        }

        /// <summary>
        /// Поворот в единичном квадрате развёртки. Разворачивается развёртка, а не картинка:
        /// тогда и стороны, которые меряются вдоль U и V, встают на свои места, и пропорции
        /// текстуры получаются верными сами собой.
        /// </summary>
        static Vector2 Turn(Vector2 uv, ScreenTurn turn) => turn switch
        {
            ScreenTurn.Deg90 => new Vector2(uv.y, 1f - uv.x),
            ScreenTurn.Deg180 => new Vector2(1f - uv.x, 1f - uv.y),
            ScreenTurn.Deg270 => new Vector2(1f - uv.y, uv.x),
            _ => uv,
        };
    }
}
