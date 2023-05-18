using UnityEngine;

public class SelfDestruct : MonoBehaviour
{
    [SerializeField]
    private float destructTime = 10f;

    public float DestructTime { set { destructTime = value; } }

    private void Start()
    {
        Invoke(nameof(DestroyGameObject), destructTime);
    }

    private void DestroyGameObject()
    {
        Destroy(gameObject);
    }
}
