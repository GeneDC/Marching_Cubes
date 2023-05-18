using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ShootProjectile : MonoBehaviour
{
    public PrimitiveType primitiveType = PrimitiveType.Sphere;
    public float shootingForce = 100f;

    public InputAction mouseClickAction;
    public List<Material> materials;

    void Start()
    {
        mouseClickAction.Enable();
        mouseClickAction.performed += OnMouseClick;
    }

    private void OnMouseClick(InputAction.CallbackContext context)
    {
        if (context.ReadValue<float>() != 1f)
        {
            return;
        }

        Camera camera = gameObject.GetComponent<Camera>();
        Vector3 direction = camera.ScreenPointToRay(Input.mousePosition).direction;

        GameObject projectile = GameObject.CreatePrimitive(primitiveType);

        SelfDestruct selfDestruct = projectile.AddComponent<SelfDestruct>();
        selfDestruct.DestructTime = 10f;

        int randomMatIndex = Random.Range(0, materials.Count);
        projectile.GetComponent<MeshRenderer>().material = materials[randomMatIndex];

        projectile.transform.position = transform.position;
        Rigidbody projectileRigidbody = projectile.AddComponent<Rigidbody>();
        projectileRigidbody.AddForce(direction * shootingForce, ForceMode.Impulse);
    }

    void OnDisable()
    {
        mouseClickAction.Disable();
    }
}