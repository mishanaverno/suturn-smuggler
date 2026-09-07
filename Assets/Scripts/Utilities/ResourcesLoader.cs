using UnityEngine;

public static class ResourcesLoader
{
    public static GameObject LoadPrefab(string path)
    {
        return Resources.Load<GameObject>($"Prefabs/{path}");
    }
}
