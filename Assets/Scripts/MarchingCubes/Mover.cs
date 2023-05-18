using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mover : MonoBehaviour
{
    [SerializeField]
    private Vector3 direction = Vector3.forward;

    [SerializeField]
    private float speed = 5.0f;

    private void Update()
    {
        transform.Translate(direction.normalized * speed * Time.deltaTime);
    }
}
