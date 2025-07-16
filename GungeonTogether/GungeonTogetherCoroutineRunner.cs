using System.Collections;
using UnityEngine;

namespace GungeonTogether
{
    public class GungeonTogetherCoroutineRunner : MonoBehaviour
    {
        private static GungeonTogetherCoroutineRunner _instance;
        public static GungeonTogetherCoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("GungeonTogetherCoroutineRunner");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<GungeonTogetherCoroutineRunner>();
                }
                return _instance;
            }
        }

        public static Coroutine RunCoroutine(IEnumerator routine)
        {
            return Instance.StartCoroutine(routine);
        }
    }
}
