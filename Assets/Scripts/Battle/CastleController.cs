
using UnityEngine;
using UnityEngine.Events;

public class CastleController : MonoBehaviour
{
    [Header("é‚Ìİ’è")]
    [SerializeField]
    private StageObjectDurability durability;

    [SerializeField]
    private GameObject visualObject;

    [Header("é‚ª”j‰ó‚³‚ê‚½‚Æ‚«‚Ì’Ê’m")]
    [SerializeField]
    private UnityEvent onCastleDestroyed;

    private void Awake()
    {
        if (durability == null)
        {
            durability = GetComponent<StageObjectDurability>();
        }
    }

    private void OnEnable()
    {
        if (durability == null)
        {
            durability = GetComponent<StageObjectDurability>();
        }

        if (durability != null)
        {
            durability.Destroyed += OnCastleDestroyed;

            if (durability.IsDestroyed)
            {
                OnCastleDestroyed();
            }
        }
    }

    private void OnDisable()
    {
        if (durability != null)
        {
            durability.Destroyed -= OnCastleDestroyed;
        }
    }

    private void OnCastleDestroyed()
    {
        if (visualObject != null)
        {
            visualObject.SetActive(false);
        }

        Debug.Log("é‚ª”j‰ó‚³‚ê‚½Bç‚Á‚Ä‚¢‚é‘¤‚Ì”s–kI");

        // Ÿ”sŠÇ—ƒVƒXƒeƒ€‚Ö’Ê’m‚·‚é
        onCastleDestroyed?.Invoke();
    }
}
