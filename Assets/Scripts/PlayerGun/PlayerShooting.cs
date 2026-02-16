using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooting : MonoBehaviour
{
    public PlayerGun gun;
    private bool isHoldingShoot = false;
    private bool isAiming = false;

    void OnShoot()
    {
        isHoldingShoot = true;
    }

    void OnAim()
    {
        isAiming = true;
    }

    void OnShootRelease()
    {
        isHoldingShoot = false;
    }

    void OnAimRelease()
    {
        isAiming = false;
    }

    void OnReload()
    {
        if (gun != null)
        {
            gun.TryReload();
        }
    }
    void Update()
    {
        if (isHoldingShoot && gun != null)
        {
            gun.Shoot();
        }
        if (isAiming && gun != null)
        {
            gun.Aim();
        }
        else
        {
            gun.StopAiming();
        }
    }
}
