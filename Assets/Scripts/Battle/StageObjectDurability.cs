
using System;
using UnityEngine;

public class StageObjectDurability : MonoBehaviour
{
    [Header("‘Ï‹v’lİ’è")]
    [SerializeField, Min(1)]
    private int maxDurability = 100;

    private int currentDurability;

    public int MaxDurability => maxDurability;
    public int CurrentDurability => currentDurability;
    public bool IsDestroyed { get; private set; }

    // ”j‰ó‚³‚ê‚½‚Æ‚«‚ÉA–å‚âé‚Ö’m‚ç‚¹‚é
    public event Action Destroyed;

    private void Awake()
    {
        currentDurability = maxDurability;
    }

    public void TakeDamage(int damage)
    {
        // ‚·‚Å‚É”j‰ó‚³‚ê‚Ä‚¢‚éê‡‚Í‰½‚à‚µ‚È‚¢
        if (IsDestroyed || damage <= 0)
        {
            return;
        }

        currentDurability = Mathf.Max(
            0,
            currentDurability - damage
        );

        Debug.Log(
            $"{gameObject.name} ‚Ì‘Ï‹v’l: " +
            $"{currentDurability}/{maxDurability}"
        );

        if (currentDurability == 0)
        {
            IsDestroyed = true;
            Debug.Log($"{gameObject.name} ‚ª”j‰ó‚³‚ê‚½I");
            Destroyed?.Invoke();
        }
    }
}
