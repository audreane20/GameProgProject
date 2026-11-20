using UnityEngine;

// The key the kid needs to unlock the front door. Walk up to it and press E.
public class KeyPickup : MonoBehaviour
{
    [SerializeField] private float pickupDistance = 1.4f;
    [SerializeField] private Transform visual;
    [SerializeField] private float spinSpeed = 70f;
    [SerializeField] private float bobHeight = 0.04f;

    private Vector3 visualStart;

    private void Start()
    {
        if (visual != null) visualStart = visual.localPosition;
    }

    private void Update()
    {
        if (visual != null)
        {
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            visual.localPosition = visualStart + Vector3.up * (Mathf.Sin(Time.time * 2f) * bobHeight);
        }

        EscapeGameManager gm = EscapeGameManager.Instance;
        if (gm == null || gm.Player == null || Time.timeScale == 0f) return;

        // measure from the kid's chest so keys on tables and on the floor both work
        Vector3 chest = gm.Player.position + Vector3.up * 0.5f;
        if (Vector3.Distance(chest, transform.position) > pickupDistance) return;

        gm.ShowPrompt("Press E to pick up the key");
        if (Input.GetKeyDown(KeyCode.E))
        {
            gm.OnKeyPickedUp();
            gameObject.SetActive(false);
        }
    }
}
