using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PoolManager : SingletonMonobehaviour<PoolManager>
{
    [SerializeField] private Pool[] poolArray = null;
    private Transform objectPoolTransform;
    private Dictionary<int, Queue<Component>> poolDictionary = new Dictionary<int, Queue<Component>>();

    [System.Serializable]
    public struct Pool
    {
        public int poolSize;
        public GameObject prefab;
        public string componentType;
    }

    protected override void Awake()
    {
        base.Awake();
        objectPoolTransform = this.gameObject.transform;
    }

    private void Start()
    {
        for(int i =0; i<poolArray.Length; i++)
        {
            CreatePool(poolArray[i].prefab, poolArray[i].poolSize, poolArray[i].componentType);
        }
    }

    /// <summary>
    /// 런타임에 풀을 동적으로 등록합니다.
    /// 이미 같은 프리팹으로 등록된 풀이 있으면 무시합니다.
    /// SoundManager 등 외부 매니저에서 Start() 시점에 호출하세요.
    /// </summary>
    public void RegisterPool(GameObject prefab, int poolSize, string componentType)
    {
        CreatePool(prefab, poolSize, componentType);
    }

    /// <summary>
    ///  Create the object pool with the specified prefabs and the specified pool size for each
    /// </summary>
    /// <param name="prefab"></param>
    /// <param name="poolSize"></param>
    /// <param name="componentType"></param>
    private void CreatePool(GameObject prefab, int poolSize, string componentType)
    {
        int poolKey = prefab.GetInstanceID();

        string prefabName = prefab.name; // get prefab name

        GameObject parentGameobject = new GameObject(prefabName + "Anchor"); // create parent gameobject to parent the child objects to

        parentGameobject.transform.SetParent(objectPoolTransform);

        if (!poolDictionary.ContainsKey(poolKey))
        {
            poolDictionary.Add(poolKey, new Queue<Component>());

            for(int i =0; i <poolSize; i++)
            {
                GameObject newObject = Instantiate(prefab, parentGameobject.transform) as GameObject;

                newObject.SetActive(false);

                poolDictionary[poolKey].Enqueue(newObject.GetComponent(Type.GetType(componentType)));
            }
        }
    }

    public Component ReuseComponent(GameObject prefab, Vector3 position, Quaternion rotaition)
    {
        int poolKey = prefab.GetInstanceID();

        if (poolDictionary.ContainsKey(poolKey))
        {
            Component componentToReuse = GetComponentFromPool(poolKey);

            ResetObject(position, rotaition, componentToReuse, prefab);

            return componentToReuse;
        }
        else
        {
            Debug.Log("No object pool for" + prefab);
            return null;
        }
    }

    /// <summary>
    ///  Get a gameobject component from the pool using the 'poolKey'
    /// </summary>
    /// <param name="poolKey"></param>
    /// <returns></returns>
    private Component GetComponentFromPool(int poolKey)
    {
        Component componentToReuse = poolDictionary[poolKey].Dequeue();
        poolDictionary[poolKey].Enqueue(componentToReuse);

        if (componentToReuse.gameObject.activeSelf == true)
        {
            componentToReuse.gameObject.SetActive(false);
        }

        return componentToReuse;
    }

    /// <summary>
    /// Reset the gameobject
    /// </summary>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <param name="componentToReuse"></param>
    /// <param name="prefab"></param>
    private void ResetObject(Vector3 position, Quaternion rotation, Component componentToReuse, GameObject prefab)
    {
        componentToReuse.transform.position = position;
        componentToReuse.transform.rotation = rotation;
        componentToReuse.gameObject.transform.localScale = prefab.transform.localScale;
    }
}