using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("Dør")]
    [Tooltip("Dørdelen som skal bevege seg. Bruker dette objektet hvis feltet er tomt.")]
    [SerializeField] private Transform doorPanel;

    [Tooltip("Hvor langt døren flyttes fra lukket posisjon, i lokale koordinater.")]
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 3f, 0f);

    [SerializeField, Min(0.01f)] private float openingSpeed = 3f;

    private Vector3 closedPosition;
    private Vector3 openPosition;
    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (doorPanel == null)
        {
            doorPanel = transform;
        }

        closedPosition = doorPanel.localPosition;
        openPosition = closedPosition + openOffset;
    }

    private void Update()
    {
        Vector3 targetPosition = isOpen ? openPosition : closedPosition;

        doorPanel.localPosition = Vector3.MoveTowards(
            doorPanel.localPosition,
            targetPosition,
            openingSpeed * Time.deltaTime
        );
    }

    public void OpenDoor()
    {
        isOpen = true;
    }

    public void CloseDoor()
    {
        isOpen = false;
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;
    }
}
