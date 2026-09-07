using UnityEngine;

namespace Utilities
{
    abstract public class ObjectWithMono<M, O> where M : MonoWithObject<O> where O : ObjectWithMono<M, O>
    {
        private GameObject _gameObject;
        private M _monoBehaviour;
        public GameObject GameObject => _gameObject;
        public M Mono => _monoBehaviour;
        protected ObjectWithMono()
        {

        }
        protected void InstatiateGameObject(GameObject prefab)
        {
            _gameObject = Object.Instantiate(prefab);
            _monoBehaviour = (M)_gameObject.AddComponent<M>().SetObject((O)this);
            _monoBehaviour.OnInstatiated();
        }
    }
    public class MonoWithObject<O> : MonoBehaviour
    {
        [SerializeField]
        protected O _object;
        public O Object => _object;
        public MonoWithObject<O> SetObject(O obj)
        {
            if (_object == null)
            {
                _object = obj;
            }
            return this;
        }

        public virtual void OnInstatiated() { }
    }
}
