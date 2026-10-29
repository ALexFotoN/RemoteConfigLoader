using UnityEngine;

public class Weapon : MonoBehaviour
{
    [Min(0)] public float damage = 10f;
    [Min(0.001f)] public float cooldown = 1f;

    public void Apply(float newDamage, float newCooldown)
    {
        damage = newDamage;
        cooldown = newCooldown;

        Debug.Log($"Weapon updated: damage={damage}, cooldown={cooldown}", this);
    }
}