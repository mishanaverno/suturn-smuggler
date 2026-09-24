using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OuterSpace
{
    /// <summary>
    /// Облака Титана: поля шума зависят только от точки сферы и параметров формы, но не от
    /// света и камеры. На каждом пикселе они стоили около 90% шейдера, поэтому считаются один
    /// раз в две кубмапы, а шейдер их только читает. Поменяли в материале форму —
    /// перезапекается; цвета и свет запекания не требуют.
    /// </summary>
    public class TitanFieldBake : IDisposable
    {
        // 1024 на грань: у Титана на высоте 400 км тексель — около 4 км, несколько пикселей
        // экрана; края облаков между текселями сглаживает бикубическая выборка в шейдере.
        const int Size = 1024;
        // Рельефу жёстких порогов не нужно: он плавный и вдвое грубее обходится без потерь.
        const int ReliefSize = 512;

        static readonly int FieldsAId = Shader.PropertyToID("_FieldsA");
        static readonly int FieldsBId = Shader.PropertyToID("_FieldsB");
        static readonly int BakedId = Shader.PropertyToID("_Baked");
        static readonly int FieldsSizeId = Shader.PropertyToID("_FieldsSize");
        static readonly int FaceId = Shader.PropertyToID("_Face");
        static readonly int EpsilonId = Shader.PropertyToID("_Epsilon");
        static readonly int[] ShapeIds =
        {
            Shader.PropertyToID("_PatternScale"), Shader.PropertyToID("_BandStretch"),
            Shader.PropertyToID("_Belts"), Shader.PropertyToID("_ClearThreshold"),
            Shader.PropertyToID("_CloudDetail"),
        };

        readonly float[] bakedShape = new float[5];
        RenderTexture fields, relief;
        Material bake;

        public void Apply(Material titan, MaterialPropertyBlock properties)
        {
            if (fields == null || ShapeChanged(titan)) Bake(titan);
            properties.SetTexture(FieldsAId, fields);
            properties.SetTexture(FieldsBId, relief);
            properties.SetFloat(BakedId, 1f);
            properties.SetFloat(FieldsSizeId, Size);
        }

        bool ShapeChanged(Material titan)
        {
            for (int i = 0; i < ShapeIds.Length; i++)
            {
                if (titan.GetFloat(ShapeIds[i]) != bakedShape[i]) return true;
            }
            return false;
        }

        void Bake(Material titan)
        {
            if (fields == null)
            {
                // Плотность режется жёсткими порогами: в 8 битах её край шёл бы ступеньками.
                fields = Cube("Titan fields", RenderTextureFormat.ARGBHalf, Size);
                relief = Cube("Titan relief", RenderTextureFormat.ARGB32, ReliefSize);
                bake = new Material(Shader.Find("Hidden/Suturn/Titan Field Bake"));
            }
            for (int i = 0; i < ShapeIds.Length; i++)
            {
                bakedShape[i] = titan.GetFloat(ShapeIds[i]);
                bake.SetFloat(ShapeIds[i], bakedShape[i]);
            }
            bake.SetFloat(EpsilonId, 1f / ReliefSize);

            CommandBuffer command = new() { name = "Titan field bake" };
            for (int face = 0; face < 6; face++)
            {
                command.SetGlobalInteger(FaceId, face);
                command.SetRenderTarget(new RenderTargetIdentifier(fields, 0, (CubemapFace)face));
                command.DrawProcedural(Matrix4x4.identity, bake, 0, MeshTopology.Triangles, 3);
                command.SetRenderTarget(new RenderTargetIdentifier(relief, 0, (CubemapFace)face));
                command.DrawProcedural(Matrix4x4.identity, bake, 1, MeshTopology.Triangles, 3);
            }
            Graphics.ExecuteCommandBuffer(command);
            command.Release();
            fields.GenerateMips();
            relief.GenerateMips();
        }

        static RenderTexture Cube(string name, RenderTextureFormat format, int size)
        {
            RenderTexture texture = new(size, size, 0, format, RenderTextureReadWrite.Linear)
            {
                name = name,
                dimension = TextureDimension.Cube,
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.Create();
            return texture;
        }

        public void Dispose()
        {
            if (fields != null) fields.Release();
            if (relief != null) relief.Release();
            UnityEngine.Object.Destroy(fields);
            UnityEngine.Object.Destroy(relief);
            UnityEngine.Object.Destroy(bake);
        }
    }
}
