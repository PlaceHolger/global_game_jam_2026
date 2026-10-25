using System.Collections.Generic;
using UnityEngine;

namespace Warmask.Ship
{
    /// <summary>
    /// Simple pool for explosion prefabs to avoid Instantiate/Destroy overhead.
    /// </summary>
    public class ExplosionPool : MonoBehaviour
    {
        public static ExplosionPool Instance { get; private set; }

        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private int initialSize = 20;

        private readonly Queue<GameObject> _pool = new Queue<GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (explosionPrefab == null) return;

            for (int i = 0; i < initialSize; i++)
            {
                var obj = Instantiate(explosionPrefab);
                obj.SetActive(false);
                _pool.Enqueue(obj);
            }
        }

        public GameObject Get(Vector3 position)
        {
            GameObject obj;
            if (_pool.Count > 0)
            {
                obj = _pool.Dequeue();
            }
            else if (explosionPrefab != null)
            {
                obj = Instantiate(explosionPrefab);
            }
            else
            {
                return null;
            }

            obj.transform.position = position;
            obj.transform.rotation = Quaternion.identity;
            obj.SetActive(true);

            // Auto-return after lifetime
            StartCoroutine(ReturnAfterDelay(obj, 2f));
            return obj;
        }

        private System.Collections.IEnumerator ReturnAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null)
            {
                obj.SetActive(false);
                _pool.Enqueue(obj);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}